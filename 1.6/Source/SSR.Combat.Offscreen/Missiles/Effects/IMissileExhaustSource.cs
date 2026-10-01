using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //向共享烟焰提供真实喷口历史，使垂发与制导弹丸复用同一套特效。
    internal interface IMissileExhaustSource
    {
        SiloSettings ExhaustSettings { get; }
        Vector3 ExhaustAnchor { get; }
        Building_MissileSilo ExhaustSilo { get; }
        GameObject FlamePrefab { get; }
        GameObject SmokePrefab { get; }
        int TrailSeed { get; }
        float FlightSeconds { get; }
        float IgnitionSeconds { get; }
        float EngineSeconds { get; }
        bool EngineIgnited { get; }
        //采样指定飞行时刻的朝向与尾喷口，转弯后保留之前的烟焰路径。
        void ExhaustPose(float seconds, out Quaternion rotation, out Vector3 tail);
    }
}
