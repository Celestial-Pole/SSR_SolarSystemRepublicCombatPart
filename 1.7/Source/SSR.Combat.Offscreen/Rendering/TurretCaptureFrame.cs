using System;
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
        internal SsrEffectsTarget Output;
        internal Vector3 MapCenter;
        internal float MapSize;
        internal float Diameter;
        internal float GroundHeight;
        internal bool NeedsDepth;
        internal int MaximumResolution = 4096;
        internal SsrLiveEntity Live;
        private int pendingResolution;
        private float pendingResolutionSince;


        //提供与离屏投影一致的默认地面锚点，供炮口和目标坐标相互转换。
        internal Vector3 GroundAnchor => new Vector3(root.position.x, root.TransformPoint(localFloor).y, root.position.z);

        //为独立弹丸和尾迹提供由世界包围范围配置的取景缓存。
        internal TurretCaptureFrame() { }

        //在预制体的默认姿态下记录尺寸，避免旋转、后坐和镜头缩放改变取景距离。
        internal TurretCaptureFrame(Transform root, Bounds bounds)
        {
            this.root = root;
            referenceScale = root.WorldScale();
            localCenter = root.InverseTransformPoint(bounds.center);
            localFloor = root.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            referenceRadius = Mathf.Max(bounds.extents.magnitude, 0.01f);
        }

        //按默认包围球设置正交相机，保留高角度俯视的侧壁厚度并固定模型比例。
        internal void Configure(SsrProjection camera, Camera mapCamera, Vector3 visibleCenter, Vector3 offset)
        {
            Vector3 scale = root.WorldScale();
            float ratio = Mathf.Max(Mathf.Abs(scale.x / referenceScale.x),
                Mathf.Max(Mathf.Abs(scale.y / referenceScale.y), Mathf.Abs(scale.z / referenceScale.z)));
            float radius = referenceRadius * ratio;
            Diameter = radius * 2;
            float distance = radius * 3;
            //高度来自默认姿态，炮管抬升或后坐不会引起整个模型忽大忽小。
            Vector3 center = root.TransformPoint(localCenter);
            center.x = visibleCenter.x;
            center.z = visibleCenter.z;
            camera.orthographicSize = radius * TurretCaptureProfile.FramingMargin;
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
        }

        //使用统一的导弹井坐标原点投影独立弹丸和烟迹，拆分采集时不改变屏幕位置。
        internal void ConfigureWorld(SsrProjection camera, Camera mapCamera, Bounds bounds, Vector3 anchor, float altitude)
        {
            GroundHeight = anchor.y;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
            Diameter = radius * 2;
            camera.orthographicSize = radius * TurretCaptureProfile.FramingMargin;
            Vector3 forward = TurretCaptureProfile.Direction * Vector3.forward;
            camera.transform.SetPositionAndRotation(bounds.center + OffscreenTurretRenderer.Offset - forward * (radius * 3), TurretCaptureProfile.Direction);
            camera.nearClipPlane = 0.001f;
            camera.farClipPlane = radius * 6 + 12;
            MapSize = camera.orthographicSize * 2;
            Vector3 delta = bounds.center - anchor;
            MapCenter = new Vector3(anchor.x + Vector3.Dot(delta, camera.transform.right), altitude,
                anchor.z + Vector3.Dot(delta, camera.transform.up));
            if (NeedsDepth) EnsureOutput(Mathf.Min(MaximumResolution,
                TurretScreenResolution.Select(mapCamera, MapCenter, MapSize, Output != null ? Output.width : 0)));
        }

        //按屏幕所需精度创建独立输出，仅进行双线性采样，不混合低分辨率纹理。
        private void EnsureOutput(int resolution)
        {
            //实体直接参加实时绘制，只有透明烟焰仍使用独立的颜色目标。
            if (!NeedsDepth) return;
            //放大立即补足精度，缩小时等待档位稳定，避免滚轮往返时反复释放和重建。
            if (Output != null && resolution < Output.width)
            {
                if (pendingResolution != resolution)
                {
                    pendingResolution = resolution;
                    pendingResolutionSince = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup - pendingResolutionSince < 0.25f) return;
            }
            pendingResolution = 0;

            if (Output != null && Output.width == resolution) return;
            Output?.Dispose();
            Output = new SsrEffectsTarget(resolution);
        }

        //引用在途帧的目标由该帧完成后释放，地图退出不直接销毁 GPU 图像。
        public void Dispose()
        {
            Live = null;
            pendingResolution = 0;
            Output?.Dispose();
            Output = null;
        }
    }
}
