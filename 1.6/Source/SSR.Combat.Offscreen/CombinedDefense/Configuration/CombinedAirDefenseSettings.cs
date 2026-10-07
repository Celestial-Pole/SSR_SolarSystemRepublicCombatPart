using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //配置弹炮合一的导弹、弹位和雷达。
    public sealed class CombinedAirDefenseSettings : GuidedMissileSettings
    {
        public List<string> missileSlots;
        public List<RadarRotationSettings> radars;
        public string missilePitchPath, missileAimPointPath;
        public Vector3 missilePitchAxis = Vector3.right;
        public Vector2 missilePitchRange = new Vector2(350, 90);
        public float missilePitchSpeed = 60, missilePitchAcceleration = 240;
        public float missileRange = 65;
        public int launchIntervalTicks = 30, reloadTicks = 600;

        //校验导弹挂点、俯仰和雷达参数。
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (missileSlots == null || missileSlots.Count != 8 || radars == null || radars.Count != 1)
                yield return "弹炮合一系统需要八个弹位和一个后部扫描雷达。";
            if (string.IsNullOrEmpty(missilePitchPath) || string.IsNullOrEmpty(missileAimPointPath)
                || missilePitchAxis.sqrMagnitude < 0.001f || missilePitchSpeed <= 0 || missilePitchAcceleration < 0)
                yield return "弹炮合一系统的导弹独立俯仰挂点、转轴或转速无效。";
            if (launchIntervalTicks < 1 || reloadTicks < 1 || missileRange <= 0)
                yield return "弹炮合一系统的射程或冷却参数无效。";
            if (radars == null) yield break;
            foreach (var radar in radars)
                if (string.IsNullOrEmpty(radar.path) || radar.axis.sqrMagnitude < 0.001f || radar.degreesPerSecond < 0)
                    yield return "弹炮合一系统的雷达路径、转轴或转速无效。";
        }
    }
}
