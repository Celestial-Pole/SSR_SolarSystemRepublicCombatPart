using System.Collections.Generic;
using Verse;

namespace SSR.Combat.Offscreen
{
    //配置弹炮合一系统的弹位、制导性能和独立雷达，不改变普通炮塔参数。
    public sealed class CombinedAirDefenseSettings : DefModExtension
    {
        public string missilePrefabPath;
        public ThingDef missileProjectile;
        public List<string> missileSlots;
        public List<RadarRotationSettings> radars;
        public float missileRange = 65;
        public int launchIntervalTicks = 30, reloadTicks = 600;
        public float ejectionSpeed = 8, ejectionClearance = 0.12f, ejectionGravity = 9.81f, ignitionDelay = 0.30f;
        public float minimumIgnitionHeight = 0.2f;
        public float maximumSpeed = 72, acceleration = 160;
        public float turnDegreesPerSecond = 240, fuseRadius = 0.6f;
        public int missileLifetimeTicks = 600;
        public SiloSettings exhaust;

        //在定义加载时报告缺失资源、挂点和无效机械参数。
        public override IEnumerable<string> ConfigErrors()
        {
            if (string.IsNullOrEmpty(missilePrefabPath) || missileProjectile == null || exhaust == null
                || string.IsNullOrEmpty(exhaust.flamePath) || string.IsNullOrEmpty(exhaust.smokePath))
                yield return "弹炮合一系统缺少导弹模型、弹丸或烟焰资源。";
            if (missileSlots == null || missileSlots.Count != 8 || radars == null || radars.Count != 2)
                yield return "弹炮合一系统需要八个弹位和两个独立雷达。";
            if (launchIntervalTicks < 1 || reloadTicks < 1 || missileLifetimeTicks < 1 || missileRange <= 0
                || ejectionSpeed <= 0 || ejectionClearance < 0 || ejectionGravity < 0 || ignitionDelay < 0
                || minimumIgnitionHeight < 0 || maximumSpeed < ejectionSpeed || acceleration < 0
                || turnDegreesPerSecond <= 0 || fuseRadius <= 0)
                yield return "弹炮合一系统的射程、冷却或制导参数无效。";
            if (radars == null) yield break;
            foreach (var radar in radars)
                if (string.IsNullOrEmpty(radar.path) || radar.axis.sqrMagnitude < 0.001f || radar.degreesPerSecond < 0)
                    yield return "弹炮合一系统的雷达路径、转轴或转速无效。";
        }
    }
}
