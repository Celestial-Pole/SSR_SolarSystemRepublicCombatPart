using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //统一正交俯视方向和地图描边宽度。
    internal static class TurretCaptureProfile
    {
        internal const float ViewElevation = 60f;
        internal const float FramingMargin = 1.12f;
        internal const float OutlineWidth = 0.05f;
        internal static readonly Quaternion Direction = Quaternion.Euler(ViewElevation, 0, 0);

    }
}
