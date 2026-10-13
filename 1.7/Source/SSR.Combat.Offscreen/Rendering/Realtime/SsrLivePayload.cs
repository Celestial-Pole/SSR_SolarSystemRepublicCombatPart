using System;
using System.Collections.Generic;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//每帧独占已经解析的绘制项，生命周期由引擎 RenderFrame 管理。
internal sealed class SsrLivePayload(int frameNumber, CameraView view, List<FinalDrawItem>[] draws, SsrEffectsFrame[] effects) : IDisposable
{
    internal readonly SsrEffectsFrame[] Effects = effects;
    internal readonly int FrameNumber = frameNumber;
    internal readonly CameraView View = view;
    internal readonly List<FinalDrawItem>[] Draws = draws;

    //帧消费完成后归还绘制项，不保留对游戏对象的引用。
    public void Dispose()
    {
        foreach (var effect in Effects) effect.Dispose();
        foreach (var list in Draws)
        {
            foreach (var draw in list) FinalDrawItem.Return(draw);
            list.Clear();
        }
    }
}
