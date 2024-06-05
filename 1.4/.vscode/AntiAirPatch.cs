using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SSRCP_Libs
{
    public class AntiAirPatch
    {
        public static void ApplyPatch(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(Building_TurretGun), "TryStartShootSomething"),
                new HarmonyMethod(typeof(AntiAirPatch), nameof(TryShootProjectile)));
            harm.Patch(AccessTools.Method(typeof(Projectile), nameof(Projectile.Launch),
                new[]
                {
                    typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
                    typeof(ProjectileHitFlags), typeof(bool), typeof(Thing),
                    typeof(ThingDef)
                }), new HarmonyMethod(typeof(AntiAirPatch), nameof(PreLaunch)));
            harm.Patch(AccessTools.Method(typeof(Projectile), "ImpactSomething"),
                new HarmonyMethod(typeof(AntiAirPatch), nameof(PreImpactSomething)));
        }

        public static bool PreImpactSomething(Projectile __instance)
        {
            var flag = false;
            switch (__instance.usedTarget.Thing)
            {
                case Projectile_Explosive { Spawned: true } proj:
                    proj.Destroy();
                    break;
                case DropPodIncoming { Spawned: true } pod:
                    pod.Destroy();
                    break;
                default:
                    flag = true;
                    break;
            }

            if (!flag)
            {
                GenClamor.DoClamor(__instance, 12f, ClamorDefOf.Impact);
                __instance.Destroy();
            }

            return flag;
        }

        public static void PreLaunch(ref LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget)
        {
            usedTarget = intendedTarget.Thing switch
            {
                Projectile_Explosive proj when Rand.Chance(0.75f) && proj.Spawned => proj,
                DropPodIncoming { Spawned: true } pod => pod,
                _ => usedTarget
            };
        }

        private static bool TryFindAntiAirTarget(Building_Turret searcher, AntiAirComp antiAirComp,
            out LocalTargetInfo target)
        {
            var range = searcher.AttackVerb.verbProps.range;
            target = LocalTargetInfo.Invalid;
            if (antiAirComp.Opts.AtProjectiles)
                target = searcher.Map.listerThings.ThingsInGroup(ThingRequestGroup.Projectile)
                    .Where(t => t is Projectile_Explosive pe && pe.Launcher.HostileTo(searcher) &&
                                t.Position.InHorDistOf(searcher.Position, range))
                    .OrderByDescending(t => t.Position.DistanceTo(searcher.Position))
                    .FirstOrDefault();

            if (target.IsValid) return true;
            if (antiAirComp.Opts.AtPods)
                target = searcher.Map.listerThings.ThingsInGroup(ThingRequestGroup.ActiveDropPod)
                    .Where(t => t is DropPodIncoming pod &&
                                (pod.Contents.innerContainer.OfType<Pawn>().FirstOrDefault()?.HostileTo(searcher) ??
                                 false) &&
                                Mathf.Abs((t.DrawPos - searcher.DrawPos).magnitude) <= range)
                    .OrderByDescending(t => Mathf.Abs((t.DrawPos - searcher.DrawPos).magnitude))
                    .FirstOrDefault();

            return target.IsValid;
        }

        public static bool TryShootProjectile(Building_TurretGun __instance, ref LocalTargetInfo ___currentTargetInt,
            ref int ___burstWarmupTicksLeft)
        {
            var antiAirComp = __instance.GetComp<AntiAirComp>();

            if (antiAirComp is null) return true;

            if (!TryFindAntiAirTarget(__instance, antiAirComp, out var target)) return antiAirComp.Opts.AtPawns;

            ___currentTargetInt = target;
            ___burstWarmupTicksLeft = __instance.def.building.turretBurstWarmupTime.RandomInRange.SecondsToTicks();

            return false;
        }
    }
}