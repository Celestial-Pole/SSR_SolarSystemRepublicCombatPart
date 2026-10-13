using System;
using UnityEngine;

namespace SSR.UnityComponent.Missiles
{
    //保存构建时采样的九舱布局、开盖时长和全部舱盖的活动包围范围。
    [Serializable]
    public sealed class SiloLayoutData
    {
        public string animatorPath;
        public float openingSeconds;
        public float clearanceHeight;
        public Vector3 captureCenter;
        public Vector3 captureSize;
        public SiloSlotData[] slots;
    }
}
