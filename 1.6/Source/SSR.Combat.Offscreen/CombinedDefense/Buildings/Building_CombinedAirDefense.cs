using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;

namespace SSR.Combat.Offscreen
{
    //协调近防炮俯仰、固定筒导弹发射和雷达扫描。
    public sealed class Building_CombinedAirDefense : Building_ConfigurableTurret, IGuidedMissileLauncher
    {
        private static readonly AccessTools.FieldRef<Building_TurretGun, bool> ReadHoldFire
            = AccessTools.FieldRefAccess<Building_TurretGun, bool>("holdFire");
        private LocalTargetInfo sharedTarget = LocalTargetInfo.Invalid;
        private CombinedMissileLauncher missiles;
        private CombinedDefenseRig rig;
        private List<float> radarAngles = new List<float> { 0 };
        internal CombinedAirDefenseSettings CombinedSettings => def.GetModExtension<CombinedAirDefenseSettings>();
        internal CombinedMissileLauncher Missiles => missiles ?? (missiles = new CombinedMissileLauncher(this));
        public override LocalTargetInfo CurrentTarget => sharedTarget;

        //返回当前导弹发射挂点。
        public Transform GetMissileFirePoint(int slot)
        {
            Aim.Prepare();
            return Rig.FirePoints[slot];
        }

        //模型重建后重新绑定机械挂点。
        internal CombinedDefenseRig Rig
        {
            get
            {
                var model = GetComp<OffscreenTurretComp>().CurrentUnityObject.ownGameObject;
                if (rig == null || rig.Model != model)
                {
                    rig = new CombinedDefenseRig(model, CombinedSettings);
                    missiles?.Prepare(rig);
                    rig.TickRadars(radarAngles, false);
                }
                return rig;
            }
        }

        //更新共享目标、近防炮、导弹和雷达。
        protected override void Tick()
        {
            bool powered = Active && !IsStunned;
            bool firing = powered && !ReadHoldFire(this);
            UpdateSharedTarget(firing);
            base.Tick();
            Rig.TickRadars(radarAngles, powered);
            Missiles.Tick(powered, firing);
        }

        //维持共享锁定，定期检查更高优先级的空中目标。
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

        //处理导弹射程内的远距离手动锁定。
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
            if (!TryReadTarget(target, out var state) || !CanReachTarget(state))
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

        //按两种武器的联合射程搜索目标。
        public override LocalTargetInfo TryFindNewTarget()
        {
            return TurretTargetSelector.Find(this, Mathf.Max(AttackVerb.EffectiveRange, CombinedSettings.missileRange));
        }

        //按联合射程和防空策略校验目标。
        public override bool TryReadTarget(LocalTargetInfo target, out TurretTargetState state)
        {
            return TurretTargetPolicy.TryRead(this, target, out state,
                Mathf.Max(AttackVerb.EffectiveRange, CombinedSettings.missileRange));
        }

        //导弹可在离筒后转向，不受近防炮俯仰限位约束。
        internal override bool CanReachTarget(TurretTargetState state)
        {
            return (state.GroundPosition - MapDrawPosition).MagnitudeHorizontalSquared()
                <= CombinedSettings.missileRange * CombinedSettings.missileRange || base.CanReachTarget(state);
        }

        //检查近防炮的开火条件。
        public override bool CanFireAt(LocalTargetInfo target)
        {
            return TurretTargetPolicy.TryRead(this, target, out var state) && Aim.Aligned(state);
        }

        //目标超出近防炮射程时保留共享锁定。
        protected override void BeginBurst()
        {
            if (!TurretTargetPolicy.TryRead(this, CurrentTarget, out _))
            { burstWarmupTicksLeft = 1; return; }
            base.BeginBurst();
        }

        //读写共享目标、导弹库存和雷达相位。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_TargetInfo.Look(ref sharedTarget, "combinedTarget");
            Scribe_Deep.Look(ref missiles, "combinedMissiles", this);
            Scribe_Collections.Look(ref radarAngles, "radarAngles", LookMode.Value);
        }

        //显示待发导弹数量。
        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n待发导弹：" + (missiles?.ReadyCount ?? 8) + " / 8（冷却自动补充）";
        }
    }
}
