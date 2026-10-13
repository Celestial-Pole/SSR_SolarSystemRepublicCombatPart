using System;
using SSR.UnityComponent.Outline;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //保存单座炮塔的固定正交取景尺度，将斜俯视影像按底座中心对齐到地图。
    internal sealed class TurretCaptureFrame : IDisposable
    {
        private readonly Transform root;
        private readonly Vector3 referenceScale;
        private readonly Vector3 localCenter;
        private readonly Vector3 localFloor;
        private readonly float referenceRadius;
        internal RenderTexture Output;
        internal RenderTexture SurfaceHeight;
        internal Vector3 MapCenter;
        internal float MapSize;
        internal float Diameter;
        internal float GroundHeight;
        internal bool NeedsDepth;
        internal int MaximumResolution = 4096;

        //提供与离屏投影一致的默认地面锚点，供炮口和目标坐标相互转换。
        internal Vector3 GroundAnchor => new Vector3(root.position.x, root.TransformPoint(localFloor).y, root.position.z);

        //为独立弹丸和尾迹提供由世界包围范围配置的取景缓存。
        internal TurretCaptureFrame() { }

        //在预制体的默认姿态下记录尺寸，避免旋转、后坐和镜头缩放改变取景距离。
        internal TurretCaptureFrame(Transform root, Bounds bounds)
        {
            this.root = root;
            referenceScale = root.lossyScale;
            localCenter = root.InverseTransformPoint(bounds.center);
            localFloor = root.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            referenceRadius = Mathf.Max(bounds.extents.magnitude, 0.01f);
        }

        //按默认包围球设置正交相机，保留高角度俯视的侧壁厚度并固定模型比例。
        internal void Configure(Camera camera, Camera mapCamera, Vector3 visibleCenter, Vector3 offset)
        {
            Vector3 scale = root.lossyScale;
            float ratio = Mathf.Max(Mathf.Abs(scale.x / referenceScale.x),
                Mathf.Max(Mathf.Abs(scale.y / referenceScale.y), Mathf.Abs(scale.z / referenceScale.z)));
            float radius = referenceRadius * ratio;
            Diameter = radius * 2;
            float distance = radius * 3;
            //高度来自默认姿态，炮管抬升或后坐不会引起整个模型忽大忽小。
            Vector3 center = root.TransformPoint(localCenter);
            center.x = visibleCenter.x;
            center.z = visibleCenter.z;
            camera.orthographic = true;
            camera.orthographicSize = radius * TurretCaptureProfile.FramingMargin;
            camera.aspect = 1;
            Vector3 forward = TurretCaptureProfile.Direction * Vector3.forward;
            camera.transform.SetPositionAndRotation(center + offset - forward * distance, TurretCaptureProfile.Direction);
            camera.nearClipPlane = Mathf.Max(0.001f, distance - radius * 1.5f);
            camera.farClipPlane = distance + radius * 1.5f;
            MapSize = 2 * camera.orthographicSize;
            //以根节点所在的底面中心作为地图锚点，抵消斜视及包围盒中心造成的偏移。
            Vector3 anchor = root.position;
            anchor.y = root.TransformPoint(localFloor).y;
            GroundHeight = anchor.y;
            Vector3 relativeAnchor = anchor - center;
            float projectedX = Vector3.Dot(relativeAnchor, camera.transform.right);
            float projectedY = Vector3.Dot(relativeAnchor, camera.transform.up);
            MapCenter = new Vector3(anchor.x - projectedX, MapProjectionPlane.Height, anchor.z - projectedY);
            int resolution = TurretScreenResolution.Select(mapCamera, MapCenter, MapSize, Output ? Output.width : 0);
            EnsureOutput(resolution);
        }

        //使用统一的导弹井坐标原点投影独立弹丸和烟迹，拆分采集时不改变屏幕位置。
        internal void ConfigureWorld(Camera camera, Camera mapCamera, Bounds bounds, Vector3 anchor, float altitude)
        {
            GroundHeight = anchor.y;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
            Diameter = radius * 2;
            camera.orthographic = true;
            camera.orthographicSize = radius * TurretCaptureProfile.FramingMargin;
            camera.aspect = 1;
            Vector3 forward = TurretCaptureProfile.Direction * Vector3.forward;
            camera.transform.SetPositionAndRotation(bounds.center + OffscreenTurretRenderer.Offset - forward * (radius * 3), TurretCaptureProfile.Direction);
            camera.nearClipPlane = 0.001f;
            camera.farClipPlane = radius * 6 + 12;
            MapSize = camera.orthographicSize * 2;
            Vector3 delta = bounds.center - anchor;
            MapCenter = new Vector3(anchor.x + Vector3.Dot(delta, camera.transform.right), altitude,
                anchor.z + Vector3.Dot(delta, camera.transform.up));
            EnsureOutput(Mathf.Min(MaximumResolution,
                TurretScreenResolution.Select(mapCamera, MapCenter, MapSize, Output ? Output.width : 0)));
        }

        //按屏幕所需精度创建独立输出，仅进行双线性采样，不混合低分辨率纹理。
        private void EnsureOutput(int resolution)
        {
            if (Output && Output.width == resolution) return;
            OutlineBuffers.Release(Output);
            Output = new RenderTexture(resolution, resolution, NeedsDepth ? 24 : 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = "SSR炮塔独立影像",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!Output.Create()) throw new InvalidOperationException("无法创建炮塔影像纹理。");
        }

        //保留实体表面高度，避免共享几何缓冲被下一座炮塔覆盖。
        internal void EnsureSurfaceHeight()
        {
            if (SurfaceHeight && SurfaceHeight.width == Output.width) return;
            OutlineBuffers.Release(SurfaceHeight);
            SurfaceHeight = new RenderTexture(Output.width, Output.height, 0,
                RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                name = "SSR炮塔表面高度",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!SurfaceHeight.Create()) throw new InvalidOperationException("无法创建炮塔表面高度纹理。");
        }

        //释放当前炮塔的影像和表面高度，中间采集缓冲由共享渲染器管理。
        public void Dispose()
        {
            OutlineBuffers.Release(Output);
            OutlineBuffers.Release(SurfaceHeight);
            Output = null;
            SurfaceHeight = null;
        }
    }
}
