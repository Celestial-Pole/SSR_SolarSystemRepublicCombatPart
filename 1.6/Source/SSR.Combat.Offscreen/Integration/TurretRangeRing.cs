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
            if (placing?.GetCompProperties<OffscreenPrefabProperties>() == null
                && selected?.GetCompProperties<OffscreenPrefabProperties>() == null) return true;
            var position = center.ToVector3Shifted();
            position.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            GenDraw.DrawCircleOutline(position, radius);
            return false;
        }
    }
}
