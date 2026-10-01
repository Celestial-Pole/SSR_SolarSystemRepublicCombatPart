using Verse;

namespace SSR.Combat.Offscreen
{
    //将目标策略的视线选项传递给原版射击流程，避免手动指令、索敌和实际开火采用不同规则。
    internal static class TurretShootLine
    {
        //仅接管配置为无需地面视线的统一炮塔，仍先检查目标种类、高度和射程。
        internal static bool Find(Verb __instance, IntVec3 root, LocalTargetInfo targ, ref ShootLine resultingLine, ref bool __result)
        {
            if (!(__instance.Caster is Building_ConfigurableTurret turret)) return true;
            if (!turret.TryReadTarget(targ, out var state))
            { resultingLine = default; __result = false; return false; }
            var settings = turret.Settings.targeting;
            bool needsSight = state.Kind == TurretTargetKind.Ground ? settings.requireGroundLineOfSight : settings.requireAirLineOfSight;
            if (needsSight) return true;
            resultingLine = new ShootLine(root, targ.Cell);
            __result = true;
            return false;
        }
    }
}
