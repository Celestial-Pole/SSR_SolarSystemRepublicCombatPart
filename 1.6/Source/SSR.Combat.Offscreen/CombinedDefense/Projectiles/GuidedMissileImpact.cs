using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SSR.Combat.Offscreen
{
    //以真实三维运动段判断接近引信、地面障碍及空中爆炸伤害。
    internal static class GuidedMissileImpact
    {
        //使用弹头与目标的相对运动扫掠，避免高速导弹越过移动目标。
        internal static bool Reached(GuidedMissileFlight flight, TurretTargetState target, float radius)
        {
            Vector3 velocity = flight.TargetPoint(new TurretTargetState(target.Kind,
                target.GroundPosition - target.Velocity.Yto0() / 60f, target.Height - target.Velocity.y / 60f, Vector3.zero));
            Vector3 from = flight.PreviousTip - velocity;
            Vector3 to = flight.Tip - flight.TargetPoint(target);
            Vector3 segment = to - from;
            float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(-Vector3.Dot(from, segment) / segment.sqrMagnitude) : 0;
            return (from + segment * fraction).sqrMagnitude <= radius * radius;
        }

        //低于地面或跨过实际建筑高度时触发碰撞，发射建筑本身不阻挡出筒。
        internal static bool GroundCollision(Projectile_GuidedDefenseMissile missile)
        {
            var flight = missile.Flight;
            if (flight.Tip.y <= flight.Anchor.y) return true;
            var from = flight.Ground(flight.PreviousTip);
            var to = flight.Ground(flight.Tip);
            foreach (var cell in GenSight.PointsOnLineOfSight(from.ToIntVec3(), to.ToIntVec3()))
            {
                if (!cell.InBounds(missile.Map)) continue;
                var building = cell.GetEdifice(missile.Map);
                if (building == null || building == missile.Launcher || building.def.Fillage != FillCategory.Full) continue;
                Vector3 segment = to - from;
                float fraction = segment.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(cell.ToVector3Shifted() - from, segment) / segment.sqrMagnitude) : 1;
                float height = Mathf.Lerp(flight.PreviousTip.y, flight.Tip.y, fraction) - flight.Anchor.y;
                if (height <= Mathf.Max(1.5f, building.def.fillPercent * 2)) return true;
            }
            return false;
        }

        //空中爆炸只影响三维半径内的空中实体，拦截弹丸和空投物不触发地面爆炸。
        internal static void AirBurst(Projectile_GuidedDefenseMissile missile)
        {
            float radius = missile.def.projectile.explosionRadius;
            foreach (var target in missile.Map.GetComponent<TurretTargetMapCache>().AirTargets(1).ToArray())
            {
                if (target == missile || !target.Spawned || target.Destroyed
                    || !TurretTargetResolver.TryRead(target, 0, out var state)) continue;
                if ((missile.Flight.TargetPoint(state) - missile.Flight.Tip).sqrMagnitude > radius * radius) continue;
                if (!TurretTargetPolicy.AirRelationAllowed(missile.Launcher, target, missile.Targeting)) continue;
                if (target is Projectile || target is Skyfaller) target.Destroy();
                else target.TakeDamage(new DamageInfo(missile.def.projectile.damageDef, missile.DamageAmount,
                    missile.ArmorPenetration, -1, missile.Launcher, weapon: missile.def));
            }
            FleckMaker.Static(missile.DrawPos, missile.Map, FleckDefOf.ExplosionFlash, radius);
            missile.def.projectile.soundExplode?.PlayOneShot(new TargetInfo(missile.Position, missile.Map));
        }
    }
}
