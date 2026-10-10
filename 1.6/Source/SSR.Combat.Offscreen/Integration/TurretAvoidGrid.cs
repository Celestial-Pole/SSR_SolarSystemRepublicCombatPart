using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SSR.Combat.Offscreen
{
    //为超出原版径向表范围的炮塔生成 AI 避让区域。
    internal static class TurretAvoidGrid
    {
        private static readonly Action<AvoidGrid, IntVec3, int> Increment =
            (Action<AvoidGrid, IntVec3, int>)Delegate.CreateDelegate(
                typeof(Action<AvoidGrid, IntVec3, int>), AccessTools.Method(typeof(AvoidGrid), "IncrementAvoidGrid"));

        //遍历地图内的射程区域，按炮塔目标策略判断是否需要视线。
        internal static bool PrintLargeRange(AvoidGrid __instance, Building_TurretGun tur)
        {
            if (!(tur is Building_ConfigurableTurret turret)) return true;
            var verb = tur.GunCompEq.PrimaryVerb;
            float radius = verb.verbProps.range + 4f;
            if (radius < GenRadial.MaxRadialPatternRadius) return true;
            float minimum = verb.verbProps.EffectiveMinRange(allowAdjacentShot: true);
            float maximumSquared = radius * radius, minimumSquared = minimum * minimum;
            bool needsSight = turret.Settings.targeting.requireGroundLineOfSight;
            Map map = __instance.map;
            CellRect area = CellRect.CenteredOn(tur.Position, Mathf.CeilToInt(radius)).ClipInsideMap(map);
            foreach (IntVec3 cell in area)
            {
                float distanceSquared = (cell - tur.Position).LengthHorizontalSquared;
                if (distanceSquared > maximumSquared || minimum >= 1f && distanceSquared <= minimumSquared) continue;
                if (cell.WalkableByNormal(map) && (!needsSight || GenSight.LineOfSight(cell, tur.Position, map, skipFirstCell: true)))
                    Increment(__instance, cell, 45);
            }
            return false;
        }
    }
}
