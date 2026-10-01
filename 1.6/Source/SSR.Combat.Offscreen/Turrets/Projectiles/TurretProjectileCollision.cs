using Verse;

namespace SSR.Combat.Offscreen
{
    //在原版实体碰撞入口过滤高度不相交的目标，保留原版命中标记和 SSR 穿透处理。
    internal static class TurretProjectileCollision
    {
        //空中目标由三维线段扫掠处理，普通弹丸继续使用原版判定。
        internal static bool CanHit(Projectile __instance, Thing thing, ref bool __result)
        {
            if (!(__instance is ITurretBallisticProjectile ballistic)
                || ballistic.Flight.AllowsGroundHit(thing, ballistic.RemainingTicks)) return true;
            __result = false;
            return false;
        }
    }
}
