using System;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //管理一枚三维飞行弹体的姿态与紧凑取景，不把尾迹计入弹体范围。
    internal sealed class MissileBodyVisual : IDisposable
    {
        internal readonly Projectile_VerticalMissile Owner;
        internal readonly TurretMeshSubmission Submission;
        private readonly GameObject root;
        private readonly Renderer[] renderers;
        private readonly Vector3 localCenter;
        private readonly float diameter;
        internal Bounds Bounds => new Bounds(root.transform.TransformPoint(localCenter), Vector3.one * diameter / Mathf.Sqrt(3));

        //按当前姿态取得实体范围，用于判断弹体与井体的投影是否仍有重叠。
        internal Bounds SurfaceBounds
        {
            get
            {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
        }

        //在真实库存位置实例化飞行模型，缩放和旋转均取自构建布局。
        internal MissileBodyVisual(Projectile_VerticalMissile owner)
        {
            Owner = owner;
            root = UnityEngine.Object.Instantiate(owner.Assets.Missile);
            root.name = "SSR飞行导弹_" + owner.thingIDNumber;
            Update();
            renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = SurfaceBounds;
            localCenter = root.transform.InverseTransformPoint(bounds.center);
            diameter = bounds.size.magnitude;
            Submission = new TurretMeshSubmission(root) { ClipGround = true };
        }

        //以弹头切线更新整个弹体，尾焰挂点跟随同一矩阵。
        internal void Update()
        {
            Owner.Trajectory.Pose(Owner.FlightSeconds, out var position, out var rotation, out _, out _);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = Owner.Trajectory.Scale;
        }

        //销毁弹体私有显示资源。
        public void Dispose()
        {
            Submission.Dispose();
            UnityEngine.Object.Destroy(root);
        }
    }
}
