using System;
using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //为使用派生射击动作的炮塔显示建造射程，支持逐发后坐对应的射击类型。
    public sealed class PlaceWorker_OffscreenTurretRadius : PlaceWorker
    {
        //读取武器的最大和最小射程并显示范围，不额外限制建筑放置。
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot,
            Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            var turret = (ThingDef)checkingDef;
            var verb = turret.building.turretGunDef.Verbs.Find(value =>
                typeof(Verb_LaunchProjectile).IsAssignableFrom(value.verbClass)
                || typeof(Verb_Spray).IsAssignableFrom(value.verbClass));
            if (verb == null)
                throw new InvalidOperationException("炮塔武器没有可显示射程的射击动作：" + turret.defName);
            if (verb.range > 0) GenDraw.DrawRadiusRing(loc, verb.range);
            var combined = turret.GetModExtension<CombinedAirDefenseSettings>();
            if (combined != null && combined.missileRange > verb.range) GenDraw.DrawRadiusRing(loc, combined.missileRange);
            if (verb.minRange > 0) GenDraw.DrawRadiusRing(loc, verb.minRange);
            return true;
        }
    }
}
