using Verse;
using Verse.AI;

namespace SSR.Combat.Offscreen
{
    //合并原版地面索敌与共享空中候选，先按目标类别优先级再按距离选择。
    internal static class TurretTargetSelector
    {
        //跳过不可达的最近目标并继续搜索，避免一个超高目标阻塞整座防空炮。
        internal static LocalTargetInfo Find(Building_ConfigurableTurret owner, float range = -1)
        {
            var settings = owner.Settings.targeting;
            LocalTargetInfo best = LocalTargetInfo.Invalid;
            int priority = int.MinValue;
            float distance = float.MaxValue;
            if (settings.targetGround)
            {
                var flags = TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
                //视线与烟雾由目标策略检查，原版中心格检查会被大型炮塔自身遮挡。
                var ground = range > 0
                    ? AttackTargetFinder.BestAttackTarget(owner, flags,
                        target => TurretTargetPolicy.GroundAutoTargetAllowed(owner, target), maxDist: range)
                    : AttackTargetFinder.BestShootTargetFromCurrentPosition(owner, flags,
                        target => TurretTargetPolicy.GroundAutoTargetAllowed(owner, target));
                if (ground != null)
                {
                    best = ground.Thing;
                    priority = settings.groundPriority;
                    distance = (ground.Thing.DrawPos - owner.MapDrawPosition).MagnitudeHorizontalSquared();
                }
            }
            if (!settings.HasAirTargets) return best;
            foreach (var target in owner.Map.GetComponent<TurretTargetMapCache>().AirTargets(settings.airScanIntervalTicks))
            {
                if (!owner.TryReadTarget(target, out var state)) continue;
                int candidatePriority = settings.Priority(state.Kind);
                float candidateDistance = (state.GroundPosition - owner.MapDrawPosition).MagnitudeHorizontalSquared();
                if (candidatePriority < priority || candidatePriority == priority && candidateDistance >= distance) continue;
                if (!owner.CanReachTarget(state)) continue;
                best = target;
                priority = candidatePriority;
                distance = candidateDistance;
            }
            return best;
        }
    }
}
