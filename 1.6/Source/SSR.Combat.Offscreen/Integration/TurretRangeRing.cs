using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //为超出原版预计算半径的 SSR 炮塔绘制圆形射程轮廓，不改变实际武器射程。
    internal static class TurretRangeRing
    {
        //仅接管正在建造或选中的 SSR 炮塔大范围提示，普通范围沿用原版网格边界。
        internal static bool DrawLargeRange(IntVec3 center, float radius)
        {
            if (radius <= GenRadial.MaxRadialPatternRadius) return true;
            var placing = (Find.DesignatorManager.SelectedDesignator as Designator_Build)?.PlacingDef as ThingDef;
            var selected = Find.Selector.SingleSelectedThing?.def;
            if (!IsOffscreenTurret(placing) && !IsOffscreenTurret(selected)) return true;
            DrawOutline(center, radius);
            return false;
        }

        //导弹井由建筑类管理模型，不携带普通炮塔的离屏组件。
        private static bool IsOffscreenTurret(ThingDef def)
        {
            return def != null && (def.GetCompProperties<OffscreenPrefabProperties>() != null
                || typeof(Building_MissileSilo).IsAssignableFrom(def.thingClass));
        }

        //建造范围直接进入本项目的绘制入口，不依赖当前选择器状态。
        internal static void Draw(IntVec3 center, float radius)
        {
            if (radius <= GenRadial.MaxRadialPatternRadius) GenDraw.DrawRadiusRing(center, radius);
            else DrawOutline(center, radius);
        }

        //大范围沿用圆形轮廓，保留武器的真实射程。
        private static void DrawOutline(IntVec3 center, float radius)
        {
            var position = center.ToVector3Shifted();
            position.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            GenDraw.DrawCircleOutline(position, radius);
        }
    }
}
