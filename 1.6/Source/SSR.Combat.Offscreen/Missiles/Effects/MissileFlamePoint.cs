using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //记录一次喷口采样的真实位置和后喷速度，使热气保留产生时的方向。
    internal struct MissileFlamePoint
    {
        internal Vector3 Position, Velocity;
        internal double BirthTime;
    }
}
