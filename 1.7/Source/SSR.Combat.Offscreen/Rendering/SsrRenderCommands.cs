using System;
using System.Collections.Generic;
using UnityEngine;
using Waylith.Rendering;
namespace SSR.Combat.Offscreen;

//冻结实体和烟焰命令，全部使用原生网格及独立的属性值。
internal sealed class SsrRenderCommands : IDisposable
{
    internal readonly List<SsrLiveDraw> CaptureDraws = new();
    internal readonly List<SsrEffectDraw> Effects = new();
    //开始下一座实体的提交。
    internal void Clear() { CaptureDraws.Clear(); Effects.Clear(); }
    //透明通道只接受单通道材质，不通过 Unity 命令缓冲或相机渲染。
    internal void DrawMesh(Waylith.Graphics.Meshes.Mesh mesh, Matrix4x4 matrix, Waylith.Rendering.Material material,
        int submesh, int pass, SsrDrawProperties properties = null)
    {
        if (pass != 0) throw new NotSupportedException("SSR 原生烟焰只接受单通道材质。");
        Effects.Add(new SsrEffectDraw(mesh, material, Matrix4x4.ToNumerics(matrix), submesh,
            properties?.Snapshot() ?? Array.Empty<DrawProperty>()));
    }
    //归还托管引用，资源由模型库和特效实例管理。
    public void Dispose() => Clear();
}

//烟焰与遮挡网格的主线程快照。
internal sealed record SsrEffectDraw(Waylith.Graphics.Meshes.Mesh Mesh, Waylith.Rendering.Material Material,
    System.Numerics.Matrix4x4 Matrix, int Submesh, DrawProperty[] Properties);
