using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理地图内导弹井、飞行弹体和余烟，将可见对象提交给现有共享相机。
    public sealed class MissileMapVisuals : MapComponent, IDisposable
    {
        private readonly HashSet<Building_MissileSilo> silos = new HashSet<Building_MissileSilo>();
        private readonly Dictionary<Projectile_VerticalMissile, MissileExhaust> missiles = new Dictionary<Projectile_VerticalMissile, MissileExhaust>();
        private readonly List<MissileExhaust> exhausts = new List<MissileExhaust>();
        private readonly Dictionary<Building_MissileSilo, SiloVisual> siloViews = new Dictionary<Building_MissileSilo, SiloVisual>();
        private readonly Dictionary<Projectile_VerticalMissile, MissileBodyVisual> bodyViews = new Dictionary<Projectile_VerticalMissile, MissileBodyVisual>();
        private bool renderFailed;
        internal bool HasContent => silos.Count != 0 || missiles.Count != 0 || exhausts.Count != 0;

        //建立地图级缓存，不为单枚导弹创建摄像机。
        public MissileMapVisuals(Map map) : base(map) { }

        //登记导弹井并唤醒共享渲染入口。
        internal void Add(Building_MissileSilo silo) { silos.Add(silo); OffscreenTurretRenderer.EnsureInstance(); }

        //登记飞行弹体和独立烟迹，离开镜头仍正常模拟。
        internal void Add(Projectile_VerticalMissile missile)
        {
            var exhaust = new MissileExhaust(missile);
            missiles.Add(missile, exhaust);
            exhausts.Add(exhaust);
            OffscreenTurretRenderer.EnsureInstance();
        }

        //回应原版可见建筑绘制通知，确保首次显示时已有共享入口。
        internal void MarkVisible(Building_MissileSilo silo) { OffscreenTurretRenderer.EnsureInstance(); }

        //移除建筑显示，已经发出的弹丸继续由地图管理。
        internal void Remove(Building_MissileSilo silo)
        {
            silos.Remove(silo);
            if (siloViews.TryGetValue(silo, out var view)) { view.Dispose(); siloViews.Remove(silo); }
        }

        //移除已结算弹体并停止发射尾焰，不清除已经留在空中的烟团。
        internal void Remove(Projectile_VerticalMissile missile)
        {
            if (missiles.TryGetValue(missile, out var exhaust)) { exhaust.Stop(); missiles.Remove(missile); }
            if (bodyViews.TryGetValue(missile, out var view)) { view.Dispose(); bodyViews.Remove(missile); }
        }

        //以游戏时间推进尾迹，暂停冻结、加速同步。
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

        //准备本帧显示，未离井弹体与井体共用深度、光影和外轮廓采集。
        internal void Draw(OffscreenTurretRenderer renderer, Camera camera)
        {
            if (renderFailed) return;
            try { DrawDisplays(renderer, camera); }
            catch (Exception exception)
            {
                renderFailed = true;
                Log.Error("[SSR离屏] 导弹显示停止：" + map + " / " + exception);
            }
        }

        //按可见范围分配模型和尾迹缓存，离开镜头的显示资源立即回收。
        private void DrawDisplays(OffscreenTurretRenderer renderer, Camera camera)
        {
            var usedSilos = new HashSet<Building_MissileSilo>();
            var usedBodies = new HashSet<Projectile_VerticalMissile>();
            foreach (var silo in silos)
            {
                var bounds = silo.CaptureBounds;
                if (!Visible(camera, bounds, silo.GroundOrigin)) continue;
                var view = GetSilo(silo, usedSilos);
                var attached = missiles.Keys.Where(m => !m.ClearOfSilo && m.SourceSilo == silo)
                    .Select(m => GetBody(m, usedBodies)).ToArray();
                renderer.DrawSilo(camera, view, attached);
            }
            foreach (var missile in missiles.Keys)
            {
                if (!missile.ClearOfSilo) continue;
                missile.Trajectory.Evaluate(missile.FlightSeconds, out var tip, out _);
                if (!Visible(camera, new Bounds(tip, Vector3.one * 3), missile.Trajectory.Anchor)) continue;
                renderer.DrawMissile(camera, GetBody(missile, usedBodies));
            }
            foreach (var exhaust in exhausts)
            {
                if (!exhaust.TryBounds(out var bounds) || !Visible(camera, bounds, exhaust.Anchor))
                { exhaust.ReleaseDisplay(); continue; }
                SiloVisual siloView = exhaust.Silo != null && exhaust.Silo.Spawned ? GetSilo(exhaust.Silo, usedSilos) : null;
                MissileBodyVisual bodyView = exhaust.Emitter != null ? GetBody(exhaust.Emitter, usedBodies) : null;
                renderer.DrawExhaust(camera, exhaust, bounds, siloView, bodyView);
            }
            foreach (var pair in siloViews.Where(p => !usedSilos.Contains(p.Key)).ToArray())
            { pair.Value.Dispose(); siloViews.Remove(pair.Key); }
            foreach (var pair in bodyViews.Where(p => !usedBodies.Contains(p.Key)).ToArray())
            { pair.Value.Dispose(); bodyViews.Remove(pair.Key); }
        }

        //按实际投影范围判断可见性，升空弹体不受原版地面格剔除限制。
        internal static bool Visible(Camera camera, Bounds bounds, Vector3 anchor)
        {
            Vector3 delta = bounds.center - anchor;
            Vector3 point = new Vector3(bounds.center.x, MapProjectionPlane.Height,
                anchor.z + Vector3.Dot(delta, TurretCaptureProfile.Direction * Vector3.up));
            Vector3 screen = camera.WorldToViewportPoint(point);
            float margin = bounds.extents.magnitude / Mathf.Max(camera.orthographicSize, 1);
            return screen.x >= -margin && screen.x <= 1 + margin && screen.y >= -margin && screen.y <= 1 + margin;
        }

        //按需实例化井体并同步当前库存和舱盖姿态。
        private SiloVisual GetSilo(Building_MissileSilo silo, HashSet<Building_MissileSilo> used)
        {
            if (!siloViews.TryGetValue(silo, out var view)) siloViews.Add(silo, view = new SiloVisual(silo));
            view.Update();
            used.Add(silo);
            return view;
        }

        //按需实例化飞行模型并同步真实轨迹姿态。
        private MissileBodyVisual GetBody(Projectile_VerticalMissile missile, HashSet<Projectile_VerticalMissile> used)
        {
            if (!bodyViews.TryGetValue(missile, out var view)) bodyViews.Add(missile, view = new MissileBodyVisual(missile));
            view.Update();
            used.Add(missile);
            return view;
        }

        //切换地图时只释放显示资源，飞行状态与烟团模拟仍在原地图继续。
        internal void ReleaseDisplays()
        {
            foreach (var view in siloViews.Values) view.Dispose();
            foreach (var view in bodyViews.Values) view.Dispose();
            foreach (var exhaust in exhausts) exhaust.ReleaseDisplay();
            siloViews.Clear();
            bodyViews.Clear();
            renderFailed = false;
        }

        //地图永久移除时清理所有实例和对实体的引用。
        public override void MapRemoved() { Dispose(); }

        //在返回主菜单或地图销毁时释放缓存。
        public void Dispose()
        {
            ReleaseDisplays();
            missiles.Clear();
            exhausts.Clear();
            silos.Clear();
        }
    }
}
