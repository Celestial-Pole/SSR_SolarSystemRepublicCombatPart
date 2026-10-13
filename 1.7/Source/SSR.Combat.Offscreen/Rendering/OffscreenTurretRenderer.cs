using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace SSR.Combat.Offscreen
{
    //收集模型数据并提交原生帧渲染，独立投影空间只用于数学计算。
    internal sealed partial class OffscreenTurretRenderer
    {
        internal static readonly Vector3 Offset = new Vector3(4096, 0, 4096);
        private static OffscreenTurretRenderer instance;
        private readonly HashSet<OffscreenTurretComp> turrets = new HashSet<OffscreenTurretComp>();
        private SsrProjection capture;
        private CommandBuffer colorCommands;
        private MapProjectionPlane plane;
        private int lastFrame = -1;
        private bool failed;
        private Map displayedMap;

        //按需创建数据渲染器并登记可见模型。
        internal static void Register(OffscreenTurretComp comp)
        {
            EnsureInstance();
            instance.turrets.Add(comp);
        }

        //允许地图中的独立导弹复用渲染器，不要求先存在普通炮塔。
        internal static void EnsureInstance()
        {
            instance ??= new OffscreenTurretRenderer();
        }

        //注销离开地图的炮塔。
        internal static void Unregister(OffscreenTurretComp comp) { if (instance != null) instance.turrets.Remove(comp); }

        //按地图释放显示资源，最后一张地图退出后释放共享命令与面片。
        internal static void ReleaseMap(Map map)
        {
            map.GetComponent<MissileMapVisuals>().Dispose();
            map.GetComponent<GuidedMissileMapVisuals>().Dispose();
            if (instance == null) return;
            foreach (var turret in instance.turrets.Where(t => t.parent.Map == map || !t.parent.Spawned).ToArray())
            {
                turret.ReleaseModel();
                instance.turrets.Remove(turret);
            }
            if (instance.turrets.Count == 0 && !Find.Maps.Any(m => m != map
                && (m.GetComponent<MissileMapVisuals>().HasContent || m.GetComponent<GuidedMissileMapVisuals>().HasContent)))
            {
                instance.Dispose();
                instance = null;
            }
        }

        //在原版地图冻结网格前采集姿态；纯数据渲染器无需 Unity 消息回调。
        internal static void CollectFrame() => instance?.BeforeCamera(Find.Camera);

        //共享资源初始化失败时停用提交；绘制失败只停用对应炮塔或地图的导弹显示。
        private void BeforeCamera(Camera camera)
        {
            if (failed || camera == null || Find.CurrentMap == null || lastFrame == Time.frameCount
                || !RimWorld.Planet.WorldRendererUtility.DrawingMap || MapGenerator.mapBeingGenerated != null
                || LongEventHandler.AnyEventWhichDoesntUseStandardWindowNowOrWaiting) return;
            lastFrame = Time.frameCount;
            if (displayedMap != Find.CurrentMap)
            {
                displayedMap?.GetComponent<MissileMapVisuals>().ReleaseDisplays();
                displayedMap?.GetComponent<GuidedMissileMapVisuals>().ReleaseDisplays();
                displayedMap = Find.CurrentMap;
            }
            foreach (var turret in turrets)
                if (Time.frameCount - turret.LastDrawFrame > 120) turret.Submission?.Frame.Dispose();
            bool any = turrets.Any(t => t.parent.Spawned && t.parent.Map == Find.CurrentMap && t.LastDrawFrame == Time.frameCount);
            var missiles = Find.CurrentMap.GetComponent<MissileMapVisuals>();
            var guided = Find.CurrentMap.GetComponent<GuidedMissileMapVisuals>();
            if (!any && !missiles.HasContent && !guided.HasContent) return;
            try
            {
                Prepare();
            }
            catch (Exception exception)
            {
                failed = true;
                Log.Error("[SSR离屏] 渲染器初始化失败：" + exception);
                return;
            }
            var offset = Matrix4x4.Translate(Offset);
            foreach (var turret in turrets)
            {
                if (turret.RenderFailed || !turret.parent.Spawned || turret.parent.Map != Find.CurrentMap
                    || turret.LastDrawFrame != Time.frameCount) continue;
                try
                {
                    turret.PrepareAnimationFrame();
                    colorCommands.Clear();
                    var submission = turret.Submission;
                    if (!submission.Draw(colorCommands, offset, out var bounds)) continue;
                    bool attachedMissile = false;
                    foreach (var body in guided.AttachedTo(turret))
                        if (body.Submission.Draw(colorCommands, offset, out var bodyBounds))
                        { bounds.Encapsulate(bodyBounds); attachedMissile = true; }
                    //出筒阶段的弹头会超出炮管边界，按合并范围取景并保持统一地面投影。
                    if (attachedMissile) submission.Frame.ConfigureWorld(capture, camera, bounds,
                        submission.Frame.GroundAnchor, MapProjectionPlane.Height);
                    else submission.Frame.Configure(capture, camera, bounds.center, Offset);
                    Render(submission.Frame, rotor: turret.BarrelSpin);
                    plane.QueueTurret(turret.parent, submission.Frame);
                }
                catch (Exception exception)
                {
                    turret.RenderFailed = true;
                    Log.Error("[SSR离屏] 炮塔绘制失败：" + turret.parent + " / " + exception);
                }
            }
            missiles.Draw(this, camera);
            guided.Draw(this, camera);
            plane.DrawTurrets(camera);
        }

        //建立原生绘制辅助资源，投影只是纯数学状态。
        private void Prepare()
        {
            if (capture != null) return;
            plane = new MapProjectionPlane();
            capture = new SsrProjection();
            colorCommands = new CommandBuffer();
        }

        //实体参加同帧的共享几何、光影和描边绘制。
        private void Render(TurretCaptureFrame frame, bool clipGround = false, TurretBarrelSpin rotor = null)
        {
            frame.Live = new SsrLiveEntity(frame, capture, colorCommands, clipGround, rotor);
        }

        //透明特效交给同一帧的原生命令缓冲，提交之后冻结投影和属性。
        private void Capture(SsrEffectsTarget target, CommandBuffer commands)
        {
            SsrLiveRenderer.SubmitEffects(target, capture.NativeView(), commands.Effects.ToArray());
        }

        //地图退出时释放持有的引用，原生附件等待所属帧完成。
        private void Dispose()
        {
            colorCommands?.Dispose();
            plane?.Dispose();
            missileOccluder?.Dispose();
            displayedMap?.GetComponent<MissileMapVisuals>().ReleaseDisplays();
            displayedMap?.GetComponent<GuidedMissileMapVisuals>().ReleaseDisplays();
        }
    }
}
