using System;
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
        internal static readonly float Height = AltitudeLayer.BuildingOnTop.AltitudeFor();

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

        //每次绘制使用独立纹理参数和变换，玩家相机仅决定面片最终的屏幕大小。
        internal void Draw(Camera camera, TurretCaptureFrame frame)
        {
            properties.SetTexture("_MainTex", frame.Output);
            var matrix = Matrix4x4.TRS(frame.MapCenter, Quaternion.identity, new Vector3(frame.MapSize, 1, frame.MapSize));
            Graphics.DrawMesh(mesh, matrix, Material, 0, camera, 0, properties,
                ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        //销毁投影面片及其私有材质。
        public void Dispose() { UnityEngine.Object.Destroy(mesh); UnityEngine.Object.Destroy(Material); }
    }
}
