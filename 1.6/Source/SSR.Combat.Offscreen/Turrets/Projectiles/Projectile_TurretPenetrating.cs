using System;
using HarmonyLib;
using HJ_SSR.Weapons;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //为电磁近防炮和轻型电磁炮保留 SSR 穿透伤害，并以炮口俯仰建立三维直线航迹。
    public sealed class Projectile_TurretPenetrating : PenetratingBullet, ITurretBallisticProjectile
    {
        private TurretProjectileFlight flight = new TurretProjectileFlight();
        private static readonly Action<PenetratingBullet, Thing, IntVec3> ApplySpatialDamage =
            (Action<PenetratingBullet, Thing, IntVec3>)Delegate.CreateDelegate(
                typeof(Action<PenetratingBullet, Thing, IntVec3>), AccessTools.Method(typeof(PenetratingBullet), "ApplyHitDamage"));
        TurretProjectileFlight ITurretBallisticProjectile.Flight => flight;
        int ITurretBallisticProjectile.RemainingTicks => ticksToImpact;
        public override int UpdateRateTicks => 1;
        public override Vector3 ExactPosition => flight.GroundPosition(ticksToImpact, def.Altitude);
        public override Vector3 DrawPos => flight.DrawPosition(ticksToImpact, def.Altitude);
        public override Quaternion ExactRotation => flight.Rotation;

        //保留原版发射信息和 SSR 穿透初始化，随后以真实炮口覆盖二维飞行计时。
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            flight.Initialize((Building_ConfigurableTurret)launcher, usedTarget, intendedTarget,
                def.projectile.SpeedTilesPerTick, def.GetModExtension<ModExtension_PenetratingProjectile>().extraRange, true);
            this.origin = flight.GroundPosition(flight.Duration, def.Altitude);
            destination = flight.GroundPosition(0, def.Altitude);
            ticksToImpact = lifetime = flight.Duration;
        }

        //逐刻检测高空目标，再交还 SSR 地面穿透流程，空中穿透同样消耗穿透力。
        protected override void TickInterval(int delta)
        {
            for (int i = 0; i < delta && !Destroyed; i++)
            {
                if (!attackedThings.Contains(intendedTarget.Thing) && flight.SweepsAirTarget(this, intendedTarget, ticksToImpact))
                {
                    Thing hit = intendedTarget.Thing;
                    attackedThings.Add(hit);
                    //弹丸和空投物没有常规生命值，实际拦截直接移除；其他实体沿用穿透伤害。
                    if (hit is Projectile || hit is RimWorld.Skyfaller) hit.Destroy();
                    else ApplySpatialDamage(this, hit, hit.Position);
                    penetratingPower -= 0.05f;
                    if (penetratingPower <= PenetratingPowerBase)
                    {
                        ShouldTerminate = true;
                        canDestroyNow = true;
                        Destroy();
                        return;
                    }
                }
                base.TickInterval(1);
            }
        }

        //保存三维发射姿态，穿透过的实体和伤害状态由 SSR 父类保存。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref flight, "turretProjectileFlight");
        }
    }
}
