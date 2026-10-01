using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //记录烟团发射位置、游戏出生时刻和固定形态种子，不随弹体转向移动。
    internal sealed class MissileSmokePuff
    {
        internal Vector3 Position;
        internal double BirthTime;
        internal float Seed;
    }
}
