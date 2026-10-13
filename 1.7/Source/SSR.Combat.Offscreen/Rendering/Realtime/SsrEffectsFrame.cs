using System;
using System.Collections.Generic;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//同帧透明采集的全部值快照，由渲染帧持有目标和已解析命令。
internal sealed class SsrEffectsFrame : IDisposable
{
    internal readonly SsrEffectsTarget Target;
    internal readonly CameraView View;
    internal readonly List<FinalDrawItem> Draws = new();
    internal readonly SsrEffectDraw[] Commands;
    //采集与地图合成读取同一个目标，GPU 阶段之间不回读或同步等待。
    internal SsrEffectsFrame(SsrEffectsTarget target, CameraView view, SsrEffectDraw[] commands)
    {
        Target = target; View = view; Commands = commands; target.Retain();
    }
    //解析仅发生在主线程，后台渲染不读取可变节点或特效模拟。
    internal void Resolve(DrawItemFactory factory, DrawPropertyArena arena)
    {
        foreach (var command in Commands)
        {
            var draw = new MeshDrawCommand(command.Mesh, command.Material, arena.Copy(command.Properties), command.Matrix, command.Submesh);
            Draws.Add(factory.TryMake(draw, arena, UnityEngine.Shader.GlobalKeywordKey, diagnosticPrefix: "SSR烟焰")
                ?? throw new InvalidOperationException("SSR 原生烟焰命令解析失败。"));
        }
    }
    //渲染帧归还 GPU 命令和目标引用。
    public void Dispose()
    {
        foreach (var draw in Draws) FinalDrawItem.Return(draw);
        Draws.Clear();
        Target.Release();
    }
}
