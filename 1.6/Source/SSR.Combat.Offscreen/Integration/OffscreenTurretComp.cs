using System;
using FS_SSR;
using RimWorld;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理炮塔的逻辑模型和渲染缓存，保持原有炮口、转向和动画路径可用。
    public sealed class OffscreenTurretComp : TC_PrefabDrawer
    {
        internal int LastDrawFrame = -1;
        internal bool RenderFailed;
        internal TurretMeshSubmission Submission;
        private GameObject observed;
        private TurretRecoilPlayback recoil;
        private TurretBarrelSpin barrelSpin;

        //向渲染器提供当前模型的转轮轴和显示速度。
        internal TurretBarrelSpin BarrelSpin => barrelSpin;

        //在核心包创建或更换模型时隐藏自动渲染，并缓存离屏网格。
        internal void Observe(UnityGameObjectUpdater updater)
        {
            if (!updater || observed == updater.ownGameObject) return;
            Submission?.Dispose();
            observed = updater.ownGameObject;
            var settings = (OffscreenPrefabProperties)props;
            NormalizeModelPose(settings);
            recoil = string.IsNullOrEmpty(settings.recoilTransformPath) ? null
                : new TurretRecoilPlayback(observed, settings.recoilTransformPath, settings.recoilClipName);
            if (!string.IsNullOrEmpty(settings.barrelSpinTransformPath))
            {
                if (barrelSpin == null) barrelSpin = new TurretBarrelSpin(this);
                barrelSpin.Bind(observed);
            }
            Submission = new TurretMeshSubmission(observed, barrelSpin?.Node);
            RenderFailed = false;
            OffscreenTurretRenderer.Register(this);
        }

        //清除模型根节点的预倾角，保留导入轴变换和炮台转向节点，再以归正姿态计算取景边界。
        private void NormalizeModelPose(OffscreenPrefabProperties settings)
        {
            if (string.IsNullOrEmpty(settings.modelRootPath))
                throw new InvalidOperationException("炮塔未配置模型姿态根节点：" + parent.def.defName);
            var modelRoot = observed.transform.Find(settings.modelRootPath);
            if (!modelRoot)
                throw new InvalidOperationException("炮塔模型姿态根节点不存在：" + parent.def.defName + " / " + settings.modelRootPath);
            modelRoot.localRotation = Quaternion.identity;
        }

        //在成功发射后确保模型已准备就绪，再启动独立的一次性后坐。
        internal void NotifyShot()
        {
            if (string.IsNullOrEmpty(((OffscreenPrefabProperties)props).recoilTransformPath)) return;
            if (!CurrentUnityObject || recoil == null)
                throw new InvalidOperationException("炮塔发射时后坐模型尚未就绪：" + parent.def.defName);
            recoil.NotifyShot();
        }

        //随游戏刻推进后坐，暂停时不播放，游戏加速时同步加速。
        public override void CompTick()
        {
            base.CompTick();
            recoil?.Tick();
        }

        //在原版逐发计时之前更新转轮的启停和机械相位。
        internal void TickBarrel(bool hasTarget) { barrelSpin?.Tick(hasTarget); }

        //检查同步转轮是否已经绑定模型并处于实际开火位置。
        internal bool PrepareBarrelShot()
        {
            return ((OffscreenPrefabProperties)props).barrelCount == 0
                || barrelSpin != null && barrelSpin.PrepareShot();
        }

        //保存转轮运动，模型引用在显示实例创建时重新绑定。
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref barrelSpin, "barrelSpin", this);
        }

        //在离屏采集前采样转轮的刻间旋转姿态。
        internal void PrepareAnimationFrame()
        {
            barrelSpin?.UpdateFrame();
        }

        //离开地图时注销模型，阻止拾取或切换地图后留下纹理。
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            OffscreenTurretRenderer.Unregister(this);
            ReleaseModel();
        }

        //释放本组件的逻辑模型及网格缓存，不触发核心包的延迟创建入口。
        internal void ReleaseModel()
        {
            Submission?.Dispose();
            Submission = null;
            recoil = null;
            barrelSpin = null;
            if (observed) UnityEngine.Object.Destroy(observed);
            observed = null;
            LastDrawFrame = -1;
        }
    }
}
