using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //连接预制体转轮和逐发机械状态，以实际射速推进并采样显示姿态。
    internal sealed class TurretBarrelSpin : IExposable
    {
        private readonly OffscreenTurretComp owner;
        private TurretRotorMotion motion = new TurretRotorMotion();
        private Transform barrel;
        private Quaternion restRotation;
        private float lastTickTime;

        //读取当前建筑的转轮和开火配置。
        private OffscreenPrefabProperties Settings => (OffscreenPrefabProperties)owner.props;

        //将枪管数量换算为相邻开火站位之间的角度。
        private float ShotStep => Settings.barrelCount > 0 ? 360f / Settings.barrelCount : 0;

        //提供需要独立稳定照明的转轮节点，炮身和雷达沿用正常表面照明。
        internal Transform Node => barrel;

        //提供包含偏航和俯仰的世界旋转轴，转轮相位不改变轴向。
        internal Vector3 WorldAxis => barrel.TransformDirection(Settings.barrelSpinAxis).normalized;

        //记录当前显示姿态的每刻角速度，供照明按实际启停过程连续过渡。
        internal float DisplaySpeed { get; private set; }

        //绑定建筑组件，使机械运动状态能够独立保存和恢复。
        public TurretBarrelSpin(OffscreenTurretComp owner) { this.owner = owner; }

        //接管转轮动画节点，模型重新创建时保留机械相位。
        internal void Bind(GameObject model)
        {
            string path = Settings.barrelSpinTransformPath;
            barrel = model.transform.Find(path);
            if (!barrel) throw new InvalidOperationException("炮塔转管挂点不存在：" + path);
            var animator = barrel.parent.GetComponent<Animator>();
            if (animator)
            {
                animator.enabled = false;
                UnityEngine.Object.Destroy(animator);
            }
            restRotation = barrel.localRotation;
            lastTickTime = Time.realtimeSinceStartup;
            Apply(0);
        }

        //在逐发计时之前推进转轮，枪管数量为零时沿用独立固定转速。
        internal void Tick(bool hasTarget)
        {
            var turret = (RimWorld.Building_TurretGun)owner.parent;
            bool running = Settings.barrelCount > 0 ? hasTarget : turret.AttackVerb.Bursting;
            float speed = Settings.barrelCount > 0
                ? ShotStep / Math.Max(1, turret.AttackVerb.TicksBetweenBurstShots)
                : Settings.barrelSpinDegreesPerSecond / 60f;
            motion.Tick(running, speed, ShotStep, Settings.barrelFirePhaseDegrees,
                Settings.barrelSpinUpTicks, Settings.barrelSpinDownTicks);
            lastTickTime = Time.realtimeSinceStartup;
            Apply(0);
        }

        //恢复逻辑刻姿态并检查开火相位，保证弹丸从固定炮口对应的枪管射出。
        internal bool PrepareShot()
        {
            Apply(0);
            return Settings.barrelCount == 0 || motion.AtFiringPosition(ShotStep, Settings.barrelFirePhaseDegrees);
        }

        //按游戏速度采样刻间旋转，暂停时保留当前机械角度。
        internal void UpdateFrame()
        {
            float fraction = Find.TickManager.Paused ? 0 : Mathf.Clamp01(
                (Time.realtimeSinceStartup - lastTickTime) * 60f * Find.TickManager.TickRateMultiplier);
            DisplaySpeed = motion.SampleSpeed(fraction);
            Apply(fraction);
        }

        //将相位写入配置的局部旋转轴，保留原有偏航和俯仰层级。
        private void Apply(float fraction)
        {
            if (barrel) barrel.localRotation = restRotation
                * Quaternion.AngleAxis(motion.Sample(fraction), Settings.barrelSpinAxis);
        }

        //保存机械运动状态，模型引用由地图实例重新绑定。
        public void ExposeData()
        {
            Scribe_Deep.Look(ref motion, "motion");
        }
    }
}
