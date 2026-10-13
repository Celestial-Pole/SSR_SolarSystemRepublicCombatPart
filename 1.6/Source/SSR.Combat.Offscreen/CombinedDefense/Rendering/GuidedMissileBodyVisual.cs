using System;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //同步制导弹体模型并维护离屏绘制缓存。
    internal sealed class GuidedMissileBodyVisual : IDisposable
    {
        internal readonly Projectile_GuidedDefenseMissile Owner;
        internal readonly TurretMeshSubmission Submission;
        private readonly GameObject root;
        private readonly Vector3 center;
        private readonly float diameter;
        internal Bounds Bounds => new Bounds(root.transform.TransformPoint(center), Vector3.one * diameter);

        //实例化弹体并建立取景范围和网格缓存。
        internal GuidedMissileBodyVisual(Projectile_GuidedDefenseMissile owner)
        {
            Owner = owner;
            root = UnityEngine.Object.Instantiate(owner.Assets.Missile);
            root.name = "SSR制导导弹_" + owner.thingIDNumber;
            Update();
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            center = root.transform.InverseTransformPoint(bounds.center);
            diameter = bounds.size.magnitude;
            Submission = new TurretMeshSubmission(root) { ClipGround = true };
        }

        //按飞行状态同步模型姿态和尺寸。
        internal void Update()
        {
            Owner.Flight.Pose(1, Owner.Assets, out var position, out var rotation, out _);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = Owner.Flight.Scale;
        }

        //释放弹体模型和网格提交缓存。
        public void Dispose()
        {
            Submission.Dispose();
            UnityEngine.Object.Destroy(root);
        }
    }
}
