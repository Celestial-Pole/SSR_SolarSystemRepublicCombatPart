using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //保存离井扰动的随机方向和侧向冲量，计算冷弹射阶段的漂移。
    internal sealed class MissileLaunchAttitude : IExposable
    {
        private Vector3 direction;
        private float releaseTime, impulseSeconds, lateralSpeed;

        //为读档提供实例，随机扰动只从存档恢复。
        public MissileLaunchAttitude() { }

        //在发射时一次性抽取离井气流扰动，不在绘制阶段重复随机。
        internal MissileLaunchAttitude(SiloSettings settings, float releaseSeconds)
        {
            direction = Quaternion.AngleAxis(Rand.Range(0f, 360f), Vector3.up) * Vector3.forward;
            lateralSpeed = settings.coldLateralSpeedRange.RandomInRange;
            releaseTime = releaseSeconds;
            impulseSeconds = settings.ejectionDisturbanceSeconds;
        }

        //积分短时气流冲量，随后保持侧向惯性。
        private void Impulse(float seconds, out float displacement, out float velocity)
        {
            float t = Mathf.Max(0, seconds - releaseTime);
            velocity = Mathf.Clamp01(t / impulseSeconds);
            displacement = t < impulseSeconds ? t * t / (2 * impulseSeconds) : t - impulseSeconds * 0.5f;
        }

        //返回冷弹射阶段的侧向位移和速度。
        internal void ColdPose(float seconds, out Vector3 drift, out Vector3 driftVelocity)
        {
            Impulse(seconds, out float displacement, out float velocity);
            drift = direction * (lateralSpeed * displacement);
            driftVelocity = direction * (lateralSpeed * velocity);
        }

        //保存随机侧向冲量及施加时序，读档和暂停不会重置漂移。
        public void ExposeData()
        {
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref releaseTime, "releaseTime");
            Scribe_Values.Look(ref impulseSeconds, "impulseSeconds");
            Scribe_Values.Look(ref lateralSpeed, "lateralSpeed");
        }
    }
}
