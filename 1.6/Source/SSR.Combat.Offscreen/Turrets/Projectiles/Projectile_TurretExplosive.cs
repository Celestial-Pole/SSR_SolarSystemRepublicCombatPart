using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //为中型和重型电磁炮提供炮口直线弹道，保留原版爆炸伤害和爆炸特效。
    public sealed class Projectile_TurretExplosive : Projectile_Explosive, ITurretBallisticProjectile
    {
        private TurretProjectileFlight flight = new TurretProjectileFlight();
        TurretProjectileFlight ITurretBallisticProjectile.Flight => flight;
        int ITurretBallisticProjectile.RemainingTicks => ticksToImpact;
        public override int UpdateRateTicks => 1;
        public override Vector3 ExactPosition => flight.GroundPosition(ticksToImpact, def.Altitude);
        public override Vector3 DrawPos => flight.DrawPosition(ticksToImpact, def.Altitude);
        public override Quaternion ExactRotation => flight.Rotation;

        //记录原版射击信息，再按实际炮口方向和三维距离确定飞行时间。
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            flight.Initialize((Building_ConfigurableTurret)launcher, usedTarget, intendedTarget, def.projectile.SpeedTilesPerTick, 0, false);
            this.origin = flight.GroundPosition(flight.Duration, def.Altitude);
            destination = flight.GroundPosition(0, def.Altitude);
            ticksToImpact = lifetime = flight.Duration;
        }

        //逐刻推进真实航迹，保留爆炸弹父类的碰撞和引信计时。
        protected override void TickInterval(int delta)
        {
            for (int i = 0; i < delta && !Destroyed; i++) base.TickInterval(1);
        }

        //终点命中只搜索实际到达的地图格，弹道偏离时不得远程命中原目标。
        protected override void ImpactSomething()
        {
            if (flight.Height(ticksToImpact) > 1.5f) { Destroy(); return; }
            usedTarget = new LocalTargetInfo(Position);
            base.ImpactSomething();
        }

        //保存独立航迹，爆炸弹父类保存引信和原版射击信息。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref flight, "turretProjectileFlight");
        }
    }
}
