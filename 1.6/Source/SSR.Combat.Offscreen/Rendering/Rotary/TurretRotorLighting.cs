using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //按可见转速控制转轮的整周平均照明，减少切面亮暗与接触阴影的频闪。
    internal static class TurretRotorLighting
    {
        private const float AverageStartDegreesPerTick = 6f;
        private const float AverageFullDegreesPerTick = 24f;

        //每次采集重设旋转照明参数，独立导弹及没有转轮的建筑使用普通照明。
        internal static void Apply(Material material, Camera camera, TurretBarrelSpin rotor)
        {
            float blend = 0;
            Vector3 axis = Vector3.forward;
            if (rotor != null && rotor.Node)
            {
                axis = camera.worldToCameraMatrix.MultiplyVector(rotor.WorldAxis).normalized;
                float speed = Find.TickManager.Paused ? 0
                    : Mathf.Abs(rotor.DisplaySpeed) * Find.TickManager.TickRateMultiplier;
                //固定采样尺度避免帧率波动改变照明强度，启停期间保持平滑过渡。
                blend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(
                    AverageStartDegreesPerTick, AverageFullDegreesPerTick, speed));
            }
            material.SetVector("_RotorAxisDirection", axis);
            material.SetFloat("_RotorLightingBlend", blend);
        }
    }
}
