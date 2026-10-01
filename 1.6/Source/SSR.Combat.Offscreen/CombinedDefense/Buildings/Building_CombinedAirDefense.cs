using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace SSR.Combat.Offscreen
{
    //协调共享锁敌、原版近防炮连发、独立导弹补充和两个持续扫描雷达。
    public sealed class Building_CombinedAirDefense : Building_ConfigurableTurret
    {
        private static readonly AccessTools.FieldRef<Building_TurretGun, bool> ReadHoldFire
            = AccessTools.FieldRefAccess<Building_TurretGun, bool>("holdFire");
        private LocalTargetInfo sharedTarget = LocalTargetInfo.Invalid;
        private CombinedMissileLauncher missiles;
        private CombinedDefenseRig rig;
        private List<float> radarAngles = new List<float> { 0, 0 };
        internal CombinedAirDefenseSettings CombinedSettings => def.GetModExtension<CombinedAirDefenseSettings>();
        public override LocalTargetInfo CurrentTarget => sharedTarget;

        //模型重建时重新绑定预制体挂点，逻辑冷却和雷达相位保持不变。
        internal CombinedDefenseRig Rig
        {
            get
            {
                var model = GetComp<OffscreenTurretComp>().CurrentUnityObject.ownGameObject;
                if (rig == null || rig.Model != model) rig = new CombinedDefenseRig(model, CombinedSettings);
                return rig;
            }
        }

        //在近防炮连发之前确定共同目标，随后推进导弹库存和雷达。
        protected override void Tick()
        {
            bool powered = Active && !IsStunned;
            bool firing = powered && !ReadHoldFire(this);
            UpdateSharedTarget(firing);
            base.Tick();
            Rig.TickRadars(radarAngles, powered);
            if (missiles == null) missiles = new CombinedMissileLauncher(this);
            missiles.Tick(powered, firing);
        }

        //保留有效锁定，定期让更高优先级的空中威胁抢占地面目标，两种武器同时切换。
        private void UpdateSharedTarget(bool firing)
        {
            LocalTargetInfo next = sharedTarget;
            if (!firing) next = LocalTargetInfo.Invalid;
            else if (ForcedTarget.IsValid) next = TryReadTarget(ForcedTarget, out _) ? ForcedTarget : LocalTargetInfo.Invalid;
            else if (!TryReadTarget(next, out var current)) next = TryFindNewTarget();
            else if (Find.TickManager.TicksGame % Settings.targeting.airScanIntervalTicks == 0)
            {
                var candidate = TryFindNewTarget();
                if (TryReadTarget(candidate, out var state)
                    && Settings.targeting.Priority(state.Kind) > Settings.targeting.Priority(current.Kind)) next = candidate;
            }
            if (next == sharedTarget) return;
            AttackVerb.Reset();
            burstWarmupTicksLeft = 0;
            sharedTarget = currentTargetInt = next;
        }

        //远于近防炮射程的手动锁定使用导弹射程，近距离和取消命令沿用原版处理。
        public override void OrderAttack(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                base.OrderAttack(target);
                sharedTarget = currentTargetInt = LocalTargetInfo.Invalid;
                AttackVerb.Reset();
                burstWarmupTicksLeft = 0;
                return;
            }
            if ((target.Cell - Position).LengthHorizontal <= AttackVerb.EffectiveRange)
            { base.OrderAttack(target); return; }
            if (!TryReadTarget(target, out var state) || !Aim.CanReach(state))
            {
                Messages.Message("目标超出弹炮合一系统的射程、高度或机械瞄准范围。", this,
                    MessageTypeDefOf.RejectInput, false);
                return;
            }
            forcedTarget = target;
            UpdateSharedTarget(Active && !IsStunned && !ReadHoldFire(this));
            if (ReadHoldFire(this)) Messages.Message("MessageTurretWontFireBecauseHoldFire".Translate(def.label),
                this, MessageTypeDefOf.RejectInput, false);
        }

        //按两种武器的联合射程索敌，近防炮弹药不足不会阻止导弹锁定。
        public override LocalTargetInfo TryFindNewTarget()
        {
            return TurretTargetSelector.Find(this, Mathf.Max(AttackVerb.EffectiveRange, CombinedSettings.missileRange));
        }

        //共同目标使用联合射程，敌我、高度和视线仍经过共享防空策略。
        public override bool TryReadTarget(LocalTargetInfo target, out TurretTargetState state)
        {
            return TurretTargetPolicy.TryRead(this, target, out state,
                Mathf.Max(AttackVerb.EffectiveRange, CombinedSettings.missileRange));
        }

        //近防炮只在自身射程内开火，远处锁定交给导弹独立处理。
        public override bool CanFireAt(LocalTargetInfo target)
        {
            return TurretTargetPolicy.TryRead(this, target, out var state) && Aim.Aligned(state);
        }

        //远处目标维持等待，不让失败的近防炮尝试取消共享导弹目标。
        protected override void BeginBurst()
        {
            if (!TurretTargetPolicy.TryRead(this, CurrentTarget, out _))
            { burstWarmupTicksLeft = 1; return; }
            base.BeginBurst();
        }

        //保存共享锁定、导弹补充和雷达相位，已发射导弹自行保存飞行状态。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_TargetInfo.Look(ref sharedTarget, "combinedTarget");
            Scribe_Deep.Look(ref missiles, "combinedMissiles", this);
            Scribe_Collections.Look(ref radarAngles, "radarAngles", LookMode.Value);
        }

        //在原有建筑状态下显示实际可发射导弹数量。
        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n待发导弹：" + (missiles?.ReadyCount ?? 8) + " / 8（冷却自动补充）";
        }
    }
}
