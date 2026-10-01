using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //描述一个独立雷达的模型节点、局部转轴和扫描速度。
    public sealed class RadarRotationSettings
    {
        public string path;
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 60;
    }
}
