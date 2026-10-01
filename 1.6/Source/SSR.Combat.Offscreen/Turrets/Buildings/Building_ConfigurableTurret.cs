using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SSR.Combat.Offscreen
{
    //保留原版炮塔的供电、燃料、指定目标与连发流程，统一接入可配置的三维瞄准。
    public class Building_ConfigurableTurret : Building_TurretGun
    {
        private TurretAimController aim;
        private Sustainer firingSound;
        internal bool UseMuzzleOrigin;
        internal TurretAimSettings Settings => def.GetModExtension<TurretAimSettings>();
        internal TurretAimController Aim => aim ?? (aim = new TurretAimController(this));
        internal Vector3 MapDrawPosition => base.DrawPos;
        public override Vector3 DrawPos => UseMuzzleOrigin ? Aim.MuzzleMapPosition : base.DrawPos;
        protected override bool CanSetForcedTarget => Settings.allowManualTarget
            && (base.CanSetForcedTarget || mannableComp == null && Faction == Faction.OfPlayer);

        //确认配置完整后登记建筑，模型与伺服只绑定当前地图的实例。
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            if (Settings == null || Settings.targeting == null)
                throw new InvalidOperationException("炮塔缺少瞄准或目标配置：" + def.defName);
            base.SpawnSetup(map, respawningAfterLoad);
        }

        //在原版逐发计时之前更新瞄准，组件动画采样后恢复活动轴的最终姿态。
        protected override void Tick()
        {
            bool active = Active && !IsStunned && (mannableComp == null || mannableComp.MannedNow);
            Aim.Tick(active);
            GetComp<OffscreenTurretComp>()?.TickBarrel(active && burstCooldownTicksLeft <= 0 && AttackVerb.Available()
                && TryReadTarget(CurrentTarget, out _));
            base.Tick();
            Aim.Prepare();
            UpdateFiringSound();
        }

        //通过共享目标策略选择地面或空中目标。
        public override LocalTargetInfo TryFindNewTarget() { return TurretTargetSelector.Find(this); }

        //提供对外一致的目标准入读取入口。
        public virtual bool TryReadTarget(LocalTargetInfo target, out TurretTargetState state)
        {
            return TurretTargetPolicy.TryRead(this, target, out state);
        }

        //每次开火检查目标有效性与炮口角度，而非只在连发开始时检查一次。
        public virtual bool CanFireAt(LocalTargetInfo target)
        {
            return TryReadTarget(target, out var state) && Aim.Aligned(state);
        }

        //等待瞄准完成和转轮进入开火位后调用原版连发入口，无效目标交还下一次索敌。
        protected override void BeginBurst()
        {
            if (!TryReadTarget(CurrentTarget, out var state) || !Aim.CanReach(state))
            { currentTargetInt = LocalTargetInfo.Invalid; burstWarmupTicksLeft = 0; return; }
            if (!Aim.Aligned(state)) { burstWarmupTicksLeft = 1; return; }
            if (GetComp<OffscreenTurretComp>()?.PrepareBarrelShot() == false)
            { burstWarmupTicksLeft = 1; return; }
            base.BeginBurst();
        }

        //只绘制离屏建筑图形和组件覆盖层，原版二维炮塔顶部不参与绘制。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Aim.Prepare();
            if (!AllComps.Any(comp => comp.DontDrawParent())) Graphic.Draw(drawLoc, flip ? Rotation.Opposite : Rotation, this);
            Comps_DrawAt(drawLoc, flip);
            Comps_PostDraw();
        }

        //随实际连发状态维护持续射击声音，失去目标或停火时停止。
        private void UpdateFiringSound()
        {
            if (AttackVerb.state != VerbState.Bursting || Settings.shootingSoundDef == null)
            { firingSound?.End(); firingSound = null; return; }
            if (firingSound == null || firingSound.Ended) firingSound = Settings.shootingSoundDef.TrySpawnSustainer(this);
            firingSound?.Maintain();
        }

        //建筑离开地图时释放持续音效，显示资源由离屏组件清理。
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            firingSound?.End();
            firingSound = null;
            base.DeSpawn(mode);
        }

        //保存原版目标与射击状态，同时保存独立转向控制器。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref aim, "turretAim", this);
        }

        //在开发者模式显示当前机械角度，便于按型号调整 XML。
        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (DebugSettings.ShowDevGizmos) text += "\n水平角：" + Aim.Yaw.ToString("F1") + "°；俯仰角：" + Aim.Pitch.ToString("F1") + "°";
            return text;
        }
    }
}
