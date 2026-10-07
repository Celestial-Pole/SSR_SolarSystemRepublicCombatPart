using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //描述舱盖局部铰链和完全打开时的角度。
    public sealed class TubeDoorSettings
    {
        public string path;
        public Vector3 axis = Vector3.up;
        public float openAngle = 100;
    }
}
