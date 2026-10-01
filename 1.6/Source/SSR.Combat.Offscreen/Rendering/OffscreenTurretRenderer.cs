using System;
using System.Collections.Generic;
using System.Linq;
using SSR.UnityComponent.Outline;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace SSR.Combat.Offscreen
{
    //在同一 Unity 场景的远处采集可见炮塔，为地图相机提供带表面光影和外轮廓的透明纹理。
    internal sealed partial class OffscreenTurretRenderer : MonoBehaviour
    {
        internal static readonly Vector3 Offset = new Vector3(4096, 0, 4096);
        private static OffscreenTurretRenderer instance;
        private readonly HashSet<OffscreenTurretComp> turrets = new HashSet<OffscreenTurretComp>();
        private readonly TurretBufferPool bufferPool = new TurretBufferPool();
        private Camera capture;
        private Material geometryMaterial, compositeMaterial;
        private CommandBuffer colorCommands, geometryCommands;
        private MapProjectionPlane plane;
        private int lastFrame = -1;
        private bool failed;
        private Map displayedMap;

        //按需创建共享离屏相机，各炮塔轮流使用对应精度的共享采集缓冲。
        internal static void Register(OffscreenTurretComp comp)
        {
            EnsureInstance();
            instance.turrets.Add(comp);
        }

        //允许地图中的独立导弹复用渲染器，不要求先存在普通炮塔。
        internal static void EnsureInstance()
        {
            if (!instance)
            {
                var root = new GameObject("SSR炮塔离屏空间");
                DontDestroyOnLoad(root);
                instance = root.AddComponent<OffscreenTurretRenderer>();
            }
        }

        //注销离开地图的炮塔。
        internal static void Unregister(OffscreenTurretComp comp) { if (instance) instance.turrets.Remove(comp); }

        //按地图释放缓存，最后一张地图退出后销毁离屏相机和纹理。
        internal static void ReleaseMap(Map map)
        {
            map.GetComponent<MissileMapVisuals>().Dispose();
            map.GetComponent<GuidedMissileMapVisuals>().Dispose();
            if (!instance) return;
            foreach (var turret in instance.turrets.Where(t => t.parent.Map == map || !t.parent.Spawned).ToArray())
            {
                turret.ReleaseModel();
                instance.turrets.Remove(turret);
            }
            if (instance.turrets.Count == 0 && !Find.Maps.Any(m => m != map
                && (m.GetComponent<MissileMapVisuals>().HasContent || m.GetComponent<GuidedMissileMapVisuals>().HasContent)))
            {
                Camera.onPreCull -= instance.BeforeCamera;
                Destroy(instance.gameObject);
                instance = null;
            }
        }

        //在地图相机准备剔除前提交面片，离屏相机的手动渲染不会递归进入。
        private void OnEnable() { Camera.onPreCull += BeforeCamera; }

        //共享资源初始化失败时停用相机；绘制失败只停用对应炮塔或地图的导弹显示。
        private void BeforeCamera(Camera camera)
        {
            if (failed || camera != Find.Camera || Find.CurrentMap == null || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (displayedMap != Find.CurrentMap)
            {
                displayedMap?.GetComponent<MissileMapVisuals>().ReleaseDisplays();
                displayedMap?.GetComponent<GuidedMissileMapVisuals>().ReleaseDisplays();
                displayedMap = Find.CurrentMap;
            }
            bufferPool.ReleaseUnused();
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
                    geometryCommands.Clear();
                    var submission = turret.Submission;
                    if (!submission.Draw(colorCommands, geometryCommands, geometryMaterial, offset, out var bounds)) continue;
                    bool attachedMissile = false;
                    foreach (var body in guided.AttachedTo(turret))
                        if (body.Submission.Draw(colorCommands, geometryCommands, geometryMaterial, offset, out var bodyBounds))
                        { bounds.Encapsulate(bodyBounds); attachedMissile = true; }
                    //出筒阶段的弹头会超出炮管边界，按合并范围取景并保持统一地面投影。
                    if (attachedMissile) submission.Frame.ConfigureWorld(capture, camera, bounds,
                        submission.Frame.GroundAnchor, MapProjectionPlane.Height);
                    else submission.Frame.Configure(capture, camera, bounds.center, Offset);
                    Render(submission.Frame, rotor: turret.BarrelSpin);
                    plane.Draw(camera, submission.Frame);
                }
                catch (Exception exception)
                {
                    turret.RenderFailed = true;
                    Log.Error("[SSR离屏] 炮塔绘制失败：" + turret.parent + " / " + exception);
                }
            }
            missiles.Draw(this, camera);
            guided.Draw(this, camera);
        }

        //通过核心框架确保战斗资源包已装载，取得 Shader 并建立唯一的采集相机。
        private void Prepare()
        {
            if (!capture)
            {
                var bundle = CombatAssetBundle.Require();
                geometryMaterial = new Material(RequireShader(bundle, "TurretGeometry"));
                compositeMaterial = new Material(RequireShader(bundle, "TurretOutline"));
                plane = new MapProjectionPlane(RequireShader(bundle, "TurretMapPlane"));
                capture = gameObject.AddComponent<Camera>();
                capture.enabled = false;
                capture.cullingMask = 0;
                capture.clearFlags = CameraClearFlags.SolidColor;
                capture.backgroundColor = Color.clear;
                capture.allowHDR = capture.allowMSAA = capture.useOcclusionCulling = false;
                capture.renderingPath = RenderingPath.Forward;
                capture.rect = new Rect(0, 0, 1, 1);
                colorCommands = new CommandBuffer { name = "SSR炮塔颜色" };
                geometryCommands = new CommandBuffer { name = "SSR炮塔表面位置与法线" };
                Log.Message("[SSR离屏] 屏幕像素自适应采集：256–4096，近景保留原生精度，最终纹理不使用 Mipmap，共享相机 1 台。");
            }
        }

        //验证资源包中的描边 Shader，缺失资源直接报告。
        private static Shader RequireShader(AssetBundle bundle, string name)
        {
            var shader = bundle.LoadAsset<Shader>("Assets/SSR/Outline/Shaders/" + name + ".shader");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("缺失或不支持的炮塔 Shader：" + name);
            return shader;
        }

        //按游戏采集参数生成单座炮塔影像，保持预乘颜色和缩小时的连续过滤。
        private void Render(TurretCaptureFrame frame, bool clipGround = false, TurretBarrelSpin rotor = null)
        {
            int size = frame.Output.width * TurretCaptureProfile.SupersamplingFor(frame.Output.width);
            var buffers = bufferPool.Get(size);
            var previous = RenderTexture.active;
            bool previousColorWrite = GL.sRGBWrite;
            try
            {
                Capture(buffers.Color, colorCommands);
                Capture(buffers.Geometry, geometryCommands);
                compositeMaterial.SetTexture("_GeometryTex", buffers.Geometry);
                compositeMaterial.SetFloat("_ClipOutlineGround", clipGround ? 1 : 0);
                compositeMaterial.SetMatrix("_CaptureToWorld", capture.cameraToWorldMatrix);
                compositeMaterial.SetFloat("_CaptureWorldHeight", capture.orthographicSize * 2 * capture.transform.up.y);
                TurretCaptureProfile.Apply(compositeMaterial, frame.Diameter, frame.Output.width);
                TurretSurfaceLighting.Apply(compositeMaterial, capture, frame.Diameter);
                TurretRotorLighting.Apply(compositeMaterial, capture, rotor);
                if (clipGround)
                {
                    //井内合并采集与离井独立采集采用相同世界线宽和遮蔽半径。
                    compositeMaterial.SetFloat("_SilhouetteWidth", 0.012f * size / frame.MapSize);
                    compositeMaterial.SetFloat("_SurfaceContactRadius", 0.07f);
                }
                TurretSurfaceLighting.RenderContact(buffers, compositeMaterial);
                OutlineColorBlit.Draw(buffers.Color, buffers.Composite, compositeMaterial, 0);
                OutlineColorBlit.Draw(buffers.Composite, frame.Output, compositeMaterial, 1);
            }
            finally { capture.targetTexture = null; RenderTexture.active = previous; GL.sRGBWrite = previousColorWrite; }
        }

        //手动执行指定通道，并移除本次采集的命令缓冲。
        private void Capture(RenderTexture texture, CommandBuffer commands)
        {
            bool previousColorWrite = GL.sRGBWrite;
            capture.targetTexture = texture;
            GL.sRGBWrite = texture.sRGB;
            capture.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, commands);
            try { capture.Render(); }
            finally
            {
                capture.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, commands);
                GL.sRGBWrite = previousColorWrite;
            }
        }

        //退出游戏或销毁渲染器时释放所有 GPU 资源和相机事件。
        private void OnDestroy()
        {
            Camera.onPreCull -= BeforeCamera;
            colorCommands?.Dispose();
            geometryCommands?.Dispose();
            bufferPool.Dispose();
            plane?.Dispose();
            OutlineBuffers.Release(geometryMaterial);
            OutlineBuffers.Release(compositeMaterial);
            OutlineBuffers.Release(missileOccluder);
            displayedMap?.GetComponent<MissileMapVisuals>().ReleaseDisplays();
            if (instance == this) instance = null;
        }
    }
}
