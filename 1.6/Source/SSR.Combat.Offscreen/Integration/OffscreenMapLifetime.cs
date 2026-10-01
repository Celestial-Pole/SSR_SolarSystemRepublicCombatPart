using System;
using Verse;

namespace SSR.Combat.Offscreen
{
    //在地图销毁或返回主菜单时清理对应炮塔，避免共享渲染器保留旧地图引用。
    public sealed class OffscreenMapLifetime : MapComponent, IDisposable
    {
        //记录所管理的地图。
        public OffscreenMapLifetime(Map map) : base(map) { }

        //释放本地图的提交缓存和共享缓冲引用。
        public void Dispose() { OffscreenTurretRenderer.ReleaseMap(map); }
    }
}
