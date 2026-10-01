using System.Collections.Generic;
using Verse;

namespace SSR.Combat.Offscreen
{
    //提供可由多个炮塔引用的目标策略，统一目标种类、高度、优先级和视线要求。
    public sealed class TurretTargetingDef : Def
    {
        public bool targetGround = true, targetOverheadProjectiles, targetSkyfallers, targetSpatialTargets;
        public bool targetNeutralAir, targetFriendlyAir;
        public bool requireGroundLineOfSight = true, requireAirLineOfSight;
        public bool useThreeDimensionalRange;
        public float minTargetHeight, maxTargetHeight = 120, groundAimHeight;
        public float leadTimeSeconds;
        public int groundPriority, projectilePriority = 200, skyfallerPriority = 100, spatialPriority = 200;
        public int airScanIntervalTicks = 15;
        internal bool HasAirTargets => targetOverheadProjectiles || targetSkyfallers || targetSpatialTargets;

        //检查目标策略的高度、提前量和扫描频率配置。
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (minTargetHeight < 0 || maxTargetHeight < minTargetHeight || groundAimHeight < 0)
                yield return "炮塔目标高度范围或地面瞄准高度无效。";
            if (leadTimeSeconds < 0 || leadTimeSeconds > 2) yield return "炮塔提前瞄准时间必须为零至两秒。";
            if (airScanIntervalTicks < 1) yield return "空中目标扫描间隔必须大于零。";
        }

        //返回目标类别的启用开关。
        internal bool Allows(TurretTargetKind kind)
        {
            switch (kind)
            {
                case TurretTargetKind.Ground: return targetGround;
                case TurretTargetKind.OverheadProjectile: return targetOverheadProjectiles;
                case TurretTargetKind.Skyfaller: return targetSkyfallers;
                case TurretTargetKind.Spatial: return targetSpatialTargets;
                default: return false;
            }
        }

        //返回同类候选按距离排序之前使用的目标优先级，数值越大越优先。
        internal int Priority(TurretTargetKind kind)
        {
            switch (kind)
            {
                case TurretTargetKind.OverheadProjectile: return projectilePriority;
                case TurretTargetKind.Skyfaller: return skyfallerPriority;
                case TurretTargetKind.Spatial: return spatialPriority;
                default: return groundPriority;
            }
        }
    }
}
