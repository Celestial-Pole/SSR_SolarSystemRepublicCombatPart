using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Waylith.Graphics.Meshes;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//在原版建筑层合成实时描边和透明烟焰，后续光照与天气仍由游戏叠加。
internal sealed class MapProjectionPlane : IDisposable
{
    private readonly Material material;
    private readonly Mesh mesh;
    private readonly int queue;
    private readonly List<(Thing Owner, TurretCaptureFrame Frame)> turrets = new();
    internal static readonly float Height = AltitudeLayer.BuildingOnTop.AltitudeFor();
    private static readonly float DepthRange = (AltitudeLayer.Item.AltitudeFor() - Height) * 0.5f;

    //原生网格只创建一次，平面尺寸和位置由每次绘制矩阵决定。
    internal MapProjectionPlane()
    {
        material = new Material(SsrShaders.Require("SSR/Missiles/Map"), "SSR透明地图面片");
        queue = Math.Min(ShaderDatabase.Cutout.renderQueue, MatBases.LightOverlay.renderQueue - 1);
        mesh = new Mesh(MeshArrays.Build(new[] {
            new System.Numerics.Vector3(-.5f,0,-.5f), new System.Numerics.Vector3(.5f,0,-.5f),
            new System.Numerics.Vector3(.5f,0,.5f), new System.Numerics.Vector3(-.5f,0,.5f) },
            new[] { 0,2,1,0,3,2 }, uv0: new[] {
            new System.Numerics.Vector2(0,0), new System.Numerics.Vector2(1,0),
            new System.Numerics.Vector2(1,1), new System.Numerics.Vector2(0,1) })) { DebugName = "SSR地图单位面片" };
    }

    //建筑按原先的占地和南北顺序统一排序。
    internal void QueueTurret(Thing owner, TurretCaptureFrame frame) => turrets.Add((owner, frame));
    //深度顺序只取决于建筑，俯仰和后坐不会改变整座炮塔的遮挡次序。
    internal void DrawTurrets(Camera camera)
    {
        turrets.Sort((a, b) => {
            int order = a.Owner.def.size.Area.CompareTo(b.Owner.def.size.Area);
            if (order != 0) return order;
            order = b.Owner.Position.z.CompareTo(a.Owner.Position.z);
            return order != 0 ? order : a.Owner.thingIDNumber.CompareTo(b.Owner.thingIDNumber);
        });
        try
        {
            float step = DepthRange / (turrets.Count + 1);
            for (int i = 0; i < turrets.Count; i++)
            {
                var frame = turrets[i].Frame;
                var center = frame.MapCenter;
                center.y = Height + step * (i + 1);
                Draw(camera, frame, center, 0);
            }
        }
        finally { turrets.Clear(); }
    }

    //独立弹体保留自身高度与表面深度范围。
    internal void Draw(Camera camera, TurretCaptureFrame frame) => Draw(camera, frame, frame.MapCenter, DepthRange);
    //实体合成和透明纹理均直接提交给 Waylith 地图命令队列。
    private void Draw(Camera camera, TurretCaptureFrame frame, Vector3 center, float depthRange)
    {
        if (frame.Live != null)
        {
            SsrLiveRenderer.Submit(camera, frame.Live, mesh, center, depthRange, queue);
            return;
        }
        var properties = new DrawPropertyBlock(new[] { DrawProperty.Texture(ShaderPropertyId.GetOrCreate("_MainTex"), frame.Output) });
        var matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(frame.MapSize, 1, frame.MapSize));
        //按地图图层选择阶段，默认的 AnyPass 会让彩色烟焰进入单通道水深预处理。
        MeshDrawSubmission.Enqueue(new MeshDrawCommand(mesh, material, MeshDrawSubmission.CopyProperties(properties),
            Matrix4x4.ToNumerics(matrix), 0, passId: LayerPassRouter.Resolve(0), renderQueueOverride: queue));
    }

    //地图资源使用引擎自身的延迟销毁队列释放。
    public void Dispose() { mesh.Dispose(); material.Dispose(); }
}
