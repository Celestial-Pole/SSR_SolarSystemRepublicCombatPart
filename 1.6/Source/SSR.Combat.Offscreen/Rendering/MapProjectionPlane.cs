using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace SSR.Combat.Offscreen
{
    //将每座炮塔的正交俯视影像放回地图，面片高度和队列都位于原版光照覆盖层之前。
    internal sealed class MapProjectionPlane : IDisposable
    {
        internal readonly Material Material;
        private readonly Mesh mesh;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        private readonly List<(Thing Owner, TurretCaptureFrame Frame)> turrets = new List<(Thing, TurretCaptureFrame)>();
        internal static readonly float Height = AltitudeLayer.BuildingOnTop.AltitudeFor();
        private static readonly float DepthRange = (AltitudeLayer.Item.AltitudeFor() - Height) * 0.5f;

        //建立透明地图材质，保留原版光照、雾和天气的后续叠加顺序。
        internal MapProjectionPlane(Shader shader)
        {
            Material = new Material(shader) { name = "SSR炮塔地图面片" };
            Material.renderQueue = Math.Min(ShaderDatabase.Cutout.renderQueue, MatBases.LightOverlay.renderQueue - 1);
            mesh = new Mesh { name = "SSR炮塔单位面片" };
            mesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            Log.Message("[SSR离屏] 面片高度=" + Height + "，光照高度=" + AltitudeLayer.LightingOverlay.AltitudeFor()
                + "，面片队列=" + Material.renderQueue + "，光照队列=" + MatBases.LightOverlay.renderQueue);
        }

        //收集炮塔和导弹井，统一分配整座建筑的遮挡层次。
        internal void QueueTurret(Thing owner, TurretCaptureFrame frame) { turrets.Add((owner, frame)); }

        //占地大的覆盖占地小的；同占地时，南侧覆盖北侧。
        internal void DrawTurrets(Camera camera)
        {
            turrets.Sort((a, b) =>
            {
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
                    //颜色和描边共用平面深度，炮管俯仰不改变炮塔之间的先后关系。
                    Draw(camera, frame, center, 0);
                }
            }
            finally { turrets.Clear(); }
        }

        //独立弹体保留表面深度，透明烟焰使用自身平面高度。
        internal void Draw(Camera camera, TurretCaptureFrame frame)
        {
            Draw(camera, frame, frame.MapCenter, DepthRange);
        }

        //按指定平面与表面深度提交面片，透明背景不写入深度。
        private void Draw(Camera camera, TurretCaptureFrame frame, Vector3 center, float surfaceDepthRange)
        {
            properties.SetTexture("_MainTex", frame.Output);
            properties.SetTexture("_SurfaceHeightTex", frame.SurfaceHeight ? (Texture)frame.SurfaceHeight : Texture2D.blackTexture);
            properties.SetFloat("_SurfaceDepthRange", surfaceDepthRange);
            var matrix = Matrix4x4.TRS(center, Quaternion.identity, new Vector3(frame.MapSize, 1, frame.MapSize));
            Graphics.DrawMesh(mesh, matrix, Material, 0, camera, 0, properties,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        //销毁投影面片及其私有材质。
        public void Dispose() { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(Material); }
    }
}
