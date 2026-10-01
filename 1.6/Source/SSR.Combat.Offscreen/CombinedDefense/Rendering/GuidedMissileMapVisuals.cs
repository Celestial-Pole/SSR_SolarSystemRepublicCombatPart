using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理制导弹体和余烟，出筒前与炮塔共同采集，出筒后使用共享相机独立显示。
    public sealed class GuidedMissileMapVisuals : MapComponent, IDisposable
    {
        private readonly Dictionary<Projectile_GuidedDefenseMissile, MissileExhaust> missiles
            = new Dictionary<Projectile_GuidedDefenseMissile, MissileExhaust>();
        private readonly Dictionary<Projectile_GuidedDefenseMissile, GuidedMissileBodyVisual> views
            = new Dictionary<Projectile_GuidedDefenseMissile, GuidedMissileBodyVisual>();
        private readonly List<MissileExhaust> exhausts = new List<MissileExhaust>();
        private bool renderFailed;
        internal bool HasContent => missiles.Count != 0 || exhausts.Count != 0;

        //为每张地图建立一份显示登记，不创建单弹摄像机。
        public GuidedMissileMapVisuals(Map map) : base(map) { }

        //登记飞行导弹及共用尾迹，并确保共享相机入口存在。
        internal void Add(Projectile_GuidedDefenseMissile missile)
        {
            var exhaust = new MissileExhaust(missile);
            missiles.Add(missile, exhaust);
            exhausts.Add(exhaust);
            OffscreenTurretRenderer.EnsureInstance();
        }

        //弹体销毁时停止采样，余烟按自身寿命继续消散。
        internal void Remove(Projectile_GuidedDefenseMissile missile)
        {
            if (missiles.TryGetValue(missile, out var exhaust)) { exhaust.Stop(); missiles.Remove(missile); }
            if (views.TryGetValue(missile, out var view)) { view.Dispose(); views.Remove(missile); }
        }

        //以逻辑刻更新喷口历史、烟迹年龄和热尾流，不依赖地图可见性。
        public override void MapComponentTick()
        {
            for (int i = exhausts.Count - 1; i >= 0; i--)
            {
                exhausts[i].Tick();
                if (!exhausts[i].Finished) continue;
                exhausts[i].Dispose();
                exhausts.RemoveAt(i);
            }
        }

        //返回仍在发射筒内的弹体，与发射炮塔合并颜色和深度采集。
        internal IEnumerable<GuidedMissileBodyVisual> AttachedTo(OffscreenTurretComp turret)
        {
            foreach (var missile in missiles.Keys)
                if (!missile.Clear && missile.Source == turret.parent) yield return Get(missile);
        }

        //使用共享相机绘制已经出筒的弹体和历史烟焰，错误只报告一次。
        internal void Draw(OffscreenTurretRenderer renderer, Camera camera)
        {
            if (renderFailed) return;
            try
            {
                var used = new HashSet<Projectile_GuidedDefenseMissile>();
                foreach (var missile in missiles.Keys)
                {
                    if (!missile.Clear) { if (views.ContainsKey(missile)) used.Add(missile); continue; }
                    if (!MissileMapVisuals.Visible(camera, new Bounds(missile.Flight.Tip, Vector3.one * 3), missile.Flight.Anchor)) continue;
                    renderer.DrawGuidedMissile(camera, Get(missile));
                    used.Add(missile);
                }
                foreach (var exhaust in exhausts)
                {
                    if (!exhaust.TryBounds(out var bounds) || !MissileMapVisuals.Visible(camera, bounds, exhaust.Anchor))
                    { exhaust.ReleaseDisplay(); continue; }
                    var missile = exhaust.GuidedEmitter;
                    GuidedMissileBodyVisual body = missile != null ? Get(missile) : null;
                    if (missile != null) used.Add(missile);
                    var turret = missile != null && !missile.Clear && missile.Source != null && missile.Source.Spawned
                        ? missile.Source.GetComp<OffscreenTurretComp>() : null;
                    renderer.DrawGuidedExhaust(camera, exhaust, bounds, turret, body);
                }
                foreach (var pair in views.Where(pair => !used.Contains(pair.Key)).ToArray())
                { pair.Value.Dispose(); views.Remove(pair.Key); }
            }
            catch (Exception exception)
            {
                renderFailed = true;
                Log.Error("[SSR离屏] 制导导弹绘制停止：" + exception);
            }
        }

        //按需创建单弹网格缓存，每帧同步当前真实姿态。
        private GuidedMissileBodyVisual Get(Projectile_GuidedDefenseMissile missile)
        {
            if (!views.TryGetValue(missile, out var view)) views.Add(missile, view = new GuidedMissileBodyVisual(missile));
            view.Update();
            return view;
        }

        //切换地图或返回主菜单时释放显示资源，保留正在模拟的弹丸。
        internal void ReleaseDisplays()
        {
            foreach (var view in views.Values) view.Dispose();
            views.Clear();
            foreach (var exhaust in exhausts) exhaust.ReleaseDisplay();
            renderFailed = false;
        }

        //地图永久移除时释放弹体和历史烟迹资源。
        public override void MapRemoved() { Dispose(); }

        //永久释放地图的飞行显示与尾迹登记，重复清理时所有集合已经为空。
        public void Dispose()
        {
            ReleaseDisplays();
            foreach (var exhaust in exhausts) exhaust.Dispose();
            exhausts.Clear();
            missiles.Clear();
        }
    }
}
