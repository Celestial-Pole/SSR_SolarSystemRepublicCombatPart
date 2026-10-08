using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SSR.Combat.Offscreen
{
    //统一自动搜索、手动目标和连发期间的目标准入检查。
    internal static class TurretTargetPolicy
    {
        //检查目标类型、高度、射程、空中敌我关系和配置要求的视线。
        internal static bool TryRead(Building_ConfigurableTurret owner, LocalTargetInfo target, out TurretTargetState state, float range = -1)
        {
            state = default;
            if (!owner.Spawned || !target.IsValid || target.Thing == owner
                || target.HasThing && (!target.Thing.Spawned || target.Thing.Map != owner.Map)) return false;
            var settings = owner.Settings.targeting;
            if (!owner.Map.GetComponent<TurretTargetMapCache>().TryRead(target, settings, out state)
                || !settings.Allows(state.Kind) || state.Height < settings.minTargetHeight || state.Height > settings.maxTargetHeight)
                return false;
            Vector3 delta = state.GroundPosition - owner.MapDrawPosition;
            delta.y = settings.useThreeDimensionalRange ? state.Height : 0;
            float minimum = owner.AttackVerb.verbProps.EffectiveMinRange(target, owner);
            float maximum = range > 0 ? range : owner.AttackVerb.EffectiveRange;
            if (delta.sqrMagnitude > maximum * maximum
                || delta.sqrMagnitude < minimum * minimum) return false;
            bool air = state.Kind != TurretTargetKind.Ground;
            if (air && !AirRelationAllowed(owner, target.Thing, settings)) return false;
            bool needsSight = air ? settings.requireAirLineOfSight : settings.requireGroundLineOfSight;
            return !needsSight || GenSight.LineOfSight(owner.Position, target.Cell, owner.Map, true);
        }

        //识别弹丸发射者及空投舱载员的阵营，默认不攻击友军和中立空投物。
        internal static bool AirRelationAllowed(Thing owner, Thing target, TurretTargetingDef settings)
        {
            Thing source = target is Projectile projectile ? projectile.Launcher : target;
            if (source != null && source.HostileTo(owner)) return true;
            bool friendly = source?.Faction != null;
            if (target is Skyfaller skyfaller)
            {
                foreach (var held in ThingOwnerUtility.GetAllThingsRecursively(skyfaller))
                {
                    if (held.HostileTo(owner)) return true;
                    if (held.Faction != null) friendly = true;
                }
            }
            return friendly ? settings.targetFriendlyAir : settings.targetNeutralAir;
        }

        //保留原版对囚犯和同阵营机械的自动目标排除条件。
        internal static bool GroundAutoTargetAllowed(Building_ConfigurableTurret owner, Thing target)
        {
            if (target is Pawn pawn)
            {
                if (owner.Faction == Faction.OfPlayer && pawn.IsPrisoner) return false;
                if (GenAI.MachinesLike(owner.Faction, pawn)) return false;
            }
            return owner.TryReadTarget(target, out var state) && state.Kind == TurretTargetKind.Ground && owner.CanReachTarget(state);
        }
    }
}
