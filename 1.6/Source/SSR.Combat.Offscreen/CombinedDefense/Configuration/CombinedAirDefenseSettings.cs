using System.Collections.Generic;
using Verse;

namespace SSR.Combat.Offscreen
{
    //配置弹炮合一的导弹、弹位和雷达。
    public sealed class CombinedAirDefenseSettings : GuidedMissileSettings
    {
        public List<string> missileSlots;
        public List<RadarRotationSettings> radars;
        public string missileAimPointPath;
        public float missileRange = 65;
        public int launchIntervalTicks = 30, reloadTicks = 600;

        //校验固定发射挂点和雷达参数。
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (missileSlots == null || missileSlots.Count != 8 || radars == null || radars.Count != 1)
                yield return "弹炮合一系统需要八个弹位和一个后部扫描雷达。";
            if (string.IsNullOrEmpty(missileAimPointPath))
                yield return "弹炮合一系统缺少导弹水平方位参考挂点。";
            if (launchIntervalTicks < 1 || reloadTicks < 1 || missileRange <= 0)
                yield return "弹炮合一系统的射程或冷却参数无效。";
            if (radars == null) yield break;
            foreach (var radar in radars)
                if (string.IsNullOrEmpty(radar.path) || radar.axis.sqrMagnitude < 0.001f || radar.degreesPerSecond < 0)
                    yield return "弹炮合一系统的雷达路径、转轴或转速无效。";
        }
    }
}
