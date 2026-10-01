using System;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //显示完整制导弹体，以弹头参考和实际尺寸同步姿态，不把烟迹计入弹体取景。
    internal sealed class GuidedMissileBodyVisual : IDisposable
    {
        internal readonly Projectile_GuidedDefenseMissile Owner;
        internal readonly TurretMeshSubmission Submission;
        private readonly GameObject root;
        private readonly Vector3 center;
        private readonly float diameter;
        internal Bounds Bounds => new Bounds(root.transform.TransformPoint(center), Vector3.one * diameter);

        //实例化重新建模的完整导弹，模型矩阵与库存弹体的出生矩阵相同。
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

        //使用弹头和尾喷口偏移还原弹体根节点，转向时模型与引信位置一致。
        internal void Update()
        {
            Owner.Flight.Pose(1, Owner.Assets, out var position, out var rotation, out _);
            root.transform.SetPositionAndRotation(position, rotation);
            root.transform.localScale = Owner.Flight.Scale;
        }

        //释放单枚导弹的离屏缓存与模型，保留共享网格和材质资源。
        public void Dispose()
        {
            Submission.Dispose();
            UnityEngine.Object.Destroy(root);
        }
    }
}
