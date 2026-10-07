using System.Collections.Generic;
using Verse;

namespace SSR.Combat.Offscreen
{
    //配置制导导弹的弹体、冷发射和尾迹。
    public class GuidedMissileSettings : DefModExtension
    {
        public string missilePrefabPath;
        public ThingDef missileProjectile;
        public float ejectionSpeed = 8, ejectionClearance = 0.12f, ejectionGravity = 9.81f, ignitionDelay = 0.30f;
        public float minimumIgnitionHeight = 0.2f;
        public float maximumSpeed = 72, acceleration = 160;
        public float turnDegreesPerSecond = 240, fuseRadius = 0.6f;
        public int missileLifetimeTicks = 600;
        public SiloSettings exhaust;

        //校验资源路径和飞行参数。
        public override IEnumerable<string> ConfigErrors()
        {
            if (string.IsNullOrEmpty(missilePrefabPath) || missileProjectile == null || exhaust == null
                || string.IsNullOrEmpty(exhaust.flamePath) || string.IsNullOrEmpty(exhaust.smokePath))
                yield return "实体导弹缺少弹体、弹丸或烟焰资源。";
            if (missileLifetimeTicks < 1 || ejectionSpeed <= 0 || ejectionClearance < 0 || ejectionGravity < 0
                || ignitionDelay < 0 || minimumIgnitionHeight < 0 || maximumSpeed < ejectionSpeed || acceleration < 0
                || turnDegreesPerSecond <= 0 || fuseRadius <= 0)
                yield return "实体导弹的冷发射或制导参数无效。";
        }
    }
}
