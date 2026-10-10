using HJ_SSR.Weapons;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //沿瞄准方向延伸电磁炮穿透弹道，在地图边缘结束飞行。
    public sealed class Projectile_RailgunPenetrating : PenetratingBullet
    {
        //保留穿透初始化，按射线与地图边界的交点确定终点。
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            Vector3 direction = (usedTarget.CenterVector3 - origin).Yto0().normalized;
            float distance = equipmentDef.Verbs[0].range + ExtraRange;
            //终点留在边缘格内，使父类完成最后一段的命中检测。
            const float inset = 0.001f;
            if (direction.x > 0)
                distance = Mathf.Min(distance, (Map.Size.x - inset - origin.x) / direction.x);
            else if (direction.x < 0)
                distance = Mathf.Min(distance, (inset - origin.x) / direction.x);
            if (direction.z > 0)
                distance = Mathf.Min(distance, (Map.Size.z - inset - origin.z) / direction.z);
            else if (direction.z < 0)
                distance = Mathf.Min(distance, (inset - origin.z) / direction.z);
            destination = origin + direction * distance;
            ticksToImpact = lifetime = Mathf.CeilToInt(StartingTicksToImpact);
        }
    }
}
