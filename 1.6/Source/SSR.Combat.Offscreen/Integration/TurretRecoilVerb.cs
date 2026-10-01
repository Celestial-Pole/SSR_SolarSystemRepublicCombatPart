using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //沿用原版命中与弹丸流程，为统一炮塔逐发检查瞄准状态并从实际炮口发射。
    public sealed class TurretRecoilVerb : Verb_Shoot
    {
        private TargetingParameters configuredTargets;

        //为当前炮塔创建独立的点选过滤器，空中目标和自动搜索采用相同准入策略。
        public override TargetingParameters targetParams
        {
            get
            {
                if (!(Caster is Building_ConfigurableTurret turret)) return base.targetParams;
                if (configuredTargets == null)
                {
                    var original = base.targetParams;
                    configuredTargets = new TargetingParameters
                    {
                        canTargetPawns = true, canTargetBuildings = true, canTargetItems = true,
                        canTargetLocations = true, mapObjectTargetsMustBeAutoAttackable = false,
                        validator = target => turret.TryReadTarget(target.HasThing ? new LocalTargetInfo(target.Thing)
                            : new LocalTargetInfo(target.Cell), out var state)
                            && (state.Kind != TurretTargetKind.Ground || original.CanTarget(target))
                    };
                }
                return configuredTargets;
            }
        }

        //空中目标使用统一策略验证高度与视线，地面目标继续经过原版射击验证。
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target)
        {
            if (Caster is Building_ConfigurableTurret turret)
            {
                if (!turret.TryReadTarget(target, out var state)) return false;
                if (state.Kind != TurretTargetKind.Ground || turret is Building_CombinedAirDefense) return true;
            }
            return base.CanHitTargetFrom(root, target);
        }

        //连发中失去瞄准即停止本轮，失败尝试不扣弹、不触发后坐。
        protected override bool TryCastShot()
        {
            var turret = Caster as Building_ConfigurableTurret;
            if (turret != null && !turret.CanFireAt(currentTarget)) return false;
            if (Caster.TryGetComp<OffscreenTurretComp>()?.PrepareBarrelShot() == false) return false;
            bool fired;
            try
            {
                if (turret != null) turret.UseMuzzleOrigin = true;
                fired = base.TryCastShot();
            }
            finally { if (turret != null) turret.UseMuzzleOrigin = false; }
            if (fired) Caster.TryGetComp<OffscreenTurretComp>()?.NotifyShot();
            return fired;
        }
    }
}
