using System;
using UnityEngine;

namespace SSR.Combat.Offscreen;

//在主线程冻结模型姿态和光照，渲染线程不访问建筑、动画节点或游戏状态。
internal sealed class SsrLiveEntity
{
    internal readonly SsrLiveDraw[] Draws;
    internal readonly Matrix4x4 View, Projection, ToWorld;
    internal readonly float MapSize, Diameter, GroundHeight, Near, Far, WorldHeight;
    internal readonly bool ClipGround;
    internal readonly Vector4 Rotor;

    //保留原采集坐标系作为投影基准，不创建相机目标或单座建筑贴图。
    internal SsrLiveEntity(TurretCaptureFrame frame, SsrProjection capture, SsrRenderCommands commands,
        bool clipGround, TurretBarrelSpin rotor)
    {
        Draws = commands.CaptureDraws.ToArray();
        View = capture.worldToCameraMatrix;
        Projection = capture.projectionMatrix;
        ToWorld = capture.cameraToWorldMatrix;
        MapSize = frame.MapSize;
        Diameter = frame.Diameter;
        GroundHeight = frame.GroundHeight;
        Near = capture.nearClipPlane;
        Far = capture.farClipPlane;
        WorldHeight = capture.orthographicSize * 2 * capture.transform.up.y;
        ClipGround = clipGround;
        Rotor = TurretRotorLighting.Parameters(capture, rotor);
    }

    //沿用哑光主辅光、转轮平均照明和接触阴影的原有参数。
    internal void SetLighting(MaterialPropertyBlock properties)
    {
        properties.SetFloat("_SilhouetteWidth", TurretCaptureProfile.OutlineWidth / MapSize);
        properties.SetFloat("_StructureWidth", 0);
        properties.SetColor("_OutlineColor", Color.black);
        properties.SetFloat("_ClipOutlineGround", ClipGround ? 1 : 0);
        properties.SetFloat("_GroundHeight", GroundHeight);
        properties.SetFloat("_CaptureWorldHeight", WorldHeight);
        properties.SetMatrix(Shader.PropertyToID("_CaptureToWorld"), ToWorld);
        properties.SetFloat("_RotorLightingBlend", Rotor.w);
        properties.SetVector("_RotorAxisDirection", new Vector4(Rotor.x, Rotor.y, Rotor.z, 0));
        properties.SetFloat("_SurfaceLighting", 1);
        properties.SetFloat("_SurfaceContactRadius", ClipGround ? 0.07f : Diameter * 0.035f);
        properties.SetFloat("_SurfaceContactStrength", 0.5f);
        properties.SetVector("_SurfaceProjection", new Vector4(Mathf.Abs(Projection.m00) * 0.5f,
            Mathf.Abs(Projection.m11) * 0.5f, 1, 0));
        properties.SetVector("_SurfaceKeyDirection", View.MultiplyVector(new Vector3(-0.6f, 1, -0.45f).normalized).normalized);
        properties.SetVector("_SurfaceFillDirection", View.MultiplyVector(new Vector3(0.8f, 0.5f, 0.2f).normalized).normalized);
        properties.SetVector("_SurfaceUpDirection", View.MultiplyVector(Vector3.up).normalized);
        //线宽以取景 UV 表示，纹理采样间距另外使用共享屏幕纹素。
        properties.SetVector("_SSRColorTexelSize", Vector4.one);
        properties.SetVector("_SSRGeometryTexelSize", Vector4.one);
    }
}
