using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SSR.Combat.Offscreen
{
    //配置筒式发射器的弹位、舱盖和装填时间。
    public sealed class TubeLauncherSettings : GuidedMissileSettings
    {
        public List<string> missileSlots;
        public List<TubeDoorSettings> doors = new List<TubeDoorSettings>();
        public int reloadTicks = 600, openingTicks = 24;
        public bool allowPartialSalvo;

        //校验弹位、舱盖和装填参数。
        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (missileSlots == null || missileSlots.Count == 0 || missileSlots.Any(string.IsNullOrEmpty)
                || missileSlots.Distinct().Count() != missileSlots.Count)
                yield return "筒式发射器必须配置不重复的实际弹位。";
            if (reloadTicks < 1 || openingTicks < 1) yield return "筒式发射器补充与开盖时间必须大于零。";
            foreach (var door in doors)
                if (string.IsNullOrEmpty(door.path) || door.axis.sqrMagnitude < 0.001f)
                    yield return "发射器舱盖的模型路径或转轴无效。";
        }
    }
}
