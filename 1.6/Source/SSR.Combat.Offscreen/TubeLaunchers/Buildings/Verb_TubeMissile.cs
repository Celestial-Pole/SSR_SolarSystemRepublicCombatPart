using System;
using Verse;

namespace SSR.Combat.Offscreen
{
    //按原版连发计时调用筒式发射器。
    public sealed class Verb_TubeMissile : TurretRecoilVerb
    {
        //本轮发射数量不超过起射时的待发库存。
        protected override int ShotsPerBurst => Math.Min(base.ShotsPerBurst, ((Building_TubeMissileTurret)caster).Launcher.ReadyCount);

        //按配置决定起射库存条件，连发期间只要求尚有余弹。
        public override bool Available()
        {
            var turret = (Building_TubeMissileTurret)caster;
            return base.Available() && (state == VerbState.Bursting ? turret.Launcher.ReadyCount > 0
                : turret.Launcher.CanStartBurst);
        }

        //按炮塔索敌策略校验目标。
        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target)
        {
            return ((Building_TubeMissileTurret)caster).TryReadTarget(target, out _);
        }

        //委托发射器发射下一枚导弹。
        protected override bool TryCastShot() => ((Building_TubeMissileTurret)caster).Launcher.TryLaunch(currentTarget);
    }
}
