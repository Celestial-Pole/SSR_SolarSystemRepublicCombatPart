using System;
using HarmonyLib;
using UnityEngine;
using UnityStubs.Rendering;
using Waylith.Graphics;
using Waylith.Rendering;
using Shader = UnityEngine.Shader;

namespace SSR.Combat.Offscreen;

//将实体实时几何与阴影步骤插入地图之前，最终描边在地图原有建筑队列中合成。
internal static class SsrLiveRenderer
{
    private static SsrLiveFeature feature;
    private static FrameRenderer frameRenderer;
    private static readonly MaterialPropertyBlock properties = new();

    //帧渲染器管理功能的在途帧和退出释放，禁止自行等待或重置游戏的提交栅栏。
    internal static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(FrameRenderer), nameof(FrameRenderer.BeginMainThreadFrame)),
            prefix: new HarmonyMethod(typeof(SsrLiveRenderer), nameof(BeginFrame)));
    }

    //在模组加载后的帧入口注册绘制阶段，屏幕附件仍在第一次显示实体时创建。
    private static void BeginFrame(FrameRenderer __instance)
    {
        frameRenderer = __instance;
        EnsureFeature();
    }

    //使用当前帧后端的设备和上下文，注册在原版地图功能之前。
    private static SsrLiveFeature EnsureFeature()
    {
        if (feature != null) return feature;
        if (frameRenderer == null) throw new InvalidOperationException("SSR 实时渲染尚未取得帧渲染器。");
        var context = Waylith.EngineApplication.Instance.RenderContext
            ?? throw new InvalidOperationException("SSR 原生窗口渲染上下文尚未初始化。");
        int index = 0;
        while (index < frameRenderer.GetFeatureCount()
            && frameRenderer.GetFeatureAt(index).GetType().FullName != "RimWorldWaylith.Rendering.MapRenderFeature") index++;
        if (index == frameRenderer.GetFeatureCount()) throw new InvalidOperationException("未找到 SSR 实时绘制需要的地图阶段。");
        feature = new SsrLiveFeature(context);
        frameRenderer.InsertFeature(index, feature);
        Verse.Log.Message("[SSR实时] 60° 固定投影，0.05 格描边；实体和阴影使用共享屏幕附件，不生成单座炮塔贴图。");
        return feature;
    }

    //烟焰采集加入同一帧，目标由帧生命周期持有。
    internal static void SubmitEffects(SsrEffectsTarget target, CameraView view, SsrEffectDraw[] commands)
        => EnsureFeature().AddEffects(new SsrEffectsFrame(target, view, commands));

    //将固定俯视坐标映射回地图，镜头只影响屏幕覆盖，不改变模型视角。
    internal static void Submit(Camera camera, SsrLiveEntity entity, Mesh quad, Vector3 center,
        float depthRange, int queue)
    {
        var live = EnsureFeature();
        var model = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(entity.MapSize, 1, entity.MapSize));
        var mapVP = camera.projectionMatrix * camera.worldToCameraMatrix;
        var captureToWorld = Matrix4x4.identity;
        captureToWorld.SetColumn(0, new Vector4(entity.MapSize, 0, 0, 0));
        captureToWorld.SetColumn(1, new Vector4(0, 0, entity.MapSize, 0));
        captureToWorld.SetColumn(2, new Vector4(0, 1, 0, 0));
        captureToWorld.SetColumn(3, new Vector4(center.x - entity.MapSize * 0.5f, center.y,
            center.z - entity.MapSize * 0.5f, 1));
        var clipToUv = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0), Quaternion.identity, new Vector3(0.5f, 0.5f, 1));
        var toScreen = clipToUv * mapVP * captureToWorld;
        //地图使用正交投影，保留二维仿射逆矩阵供屏幕像素中心回投到取景坐标。
        var uvMatrix = Matrix4x4.identity;
        uvMatrix.m00 = toScreen.m00; uvMatrix.m01 = toScreen.m01; uvMatrix.m03 = toScreen.m03;
        uvMatrix.m10 = toScreen.m10; uvMatrix.m11 = toScreen.m11; uvMatrix.m13 = toScreen.m13;
        properties.Clear();
        entity.SetLighting(properties);
        properties.SetMatrix(Shader.PropertyToID("_SSRScreenFromCapture"), uvMatrix);
        properties.SetMatrix(Shader.PropertyToID("_SSRCaptureFromScreen"), uvMatrix.inverse);
        properties.SetMatrix(Shader.PropertyToID("_SSRMapViewProjection"), mapVP);
        properties.SetFloat("_SurfaceDepthRange", depthRange);
        int id = live.Add(entity, quad, Matrix4x4.ToNumerics(model), center,
            CameraViewFactory.GetView(camera), properties);
        properties.SetFloat("_SSRObjectId", id);
        var native = live.ComposeProperties(properties);
        //按地图图层选择阶段，防止彩色合成命令进入单通道水深预处理并复用错误管线。
        MeshDrawSubmission.Enqueue(new MeshDrawCommand(quad, live.Composite,
            MeshDrawSubmission.CopyProperties(native), Matrix4x4.ToNumerics(model), 0,
            passId: LayerPassRouter.Resolve(0), renderQueueOverride: queue));
    }
}
