using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //从炮口发射弹丸，逐发检查瞄准并触发后坐。
    public class TurretRecoilVerb : Verb_Shoot
    {
        private TargetingParameters configuredTargets;

        //为当前炮塔创建点选目标过滤器。
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

        //校验空中目标；地面目标同时经过原版射击检查。
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target)
        {
            if (Caster is Building_ConfigurableTurret turret)
            {
                if (!turret.TryReadTarget(target, out var state)) return false;
                if (state.Kind != TurretTargetKind.Ground || turret is Building_CombinedAirDefense) return true;
            }
            return base.CanHitTargetFrom(root, target);
        }

        //失去瞄准时中止连发，成功射击后触发后坐。
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
