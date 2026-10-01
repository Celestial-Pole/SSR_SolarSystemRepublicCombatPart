using System;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //同步库存和九组独立舱盖动画，并为尚未离井的弹体提供同一深度采集空间。
    internal sealed class SiloVisual : IDisposable
    {
        internal readonly Building_MissileSilo Owner;
        internal readonly TurretMeshSubmission Submission;
        private readonly GameObject root, animationRoot;
        private readonly GameObject[] stored = new GameObject[9];
        internal Bounds Bounds => Owner.CaptureBounds;

        //实例化游戏包装预制体，禁用自动动画和自动渲染。
        internal SiloVisual(Building_MissileSilo owner)
        {
            Owner = owner;
            root = UnityEngine.Object.Instantiate(owner.Assets.Prefab);
            root.name = "SSR导弹井显示_" + owner.thingIDNumber;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            animationRoot = root.transform.Find(owner.Assets.Layout.animatorPath).gameObject;
            for (int i = 0; i < 9; i++) stored[i] = root.transform.Find(owner.Assets.Layout.slots[i].path).gameObject;
            Submission = new TurretMeshSubmission(root) { ClipGround = true };
            Update();
        }

        //分别采样每个舱盖的逻辑时钟，未轮到的舱盖保持闭合，关盖倒放同一独立曲线。
        internal void Update()
        {
            root.transform.SetPositionAndRotation(Owner.GroundOrigin, Owner.ModelRotation);
            for (int i = 0; i < 9; i++)
            {
                Owner.Assets.Openings[i].SampleAnimation(animationRoot, Owner.Controller.LidTime(i));
                stored[i].SetActive(Owner.Controller.IsLoaded(i));
            }
        }

        //释放地图实例和输出纹理，资源包内的原始预制体保持不变。
        public void Dispose()
        {
            Submission.Dispose();
            UnityEngine.Object.Destroy(root);
        }
    }
}
