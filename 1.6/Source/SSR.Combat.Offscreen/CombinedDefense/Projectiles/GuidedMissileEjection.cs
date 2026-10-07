using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //计算筒内弹射、离筒滑行和点火时刻。
    internal sealed class GuidedMissileEjection : IExposable
    {
        private float stroke, missileLength, releaseSpeed, gravity, releaseSeconds, ignitionSeconds;
        private float ignitionDelay, minimumIgnitionHeight;
        internal float ReleaseSeconds => releaseSeconds;
        internal float IgnitionSeconds => ignitionSeconds;

        //供存档系统创建弹射状态。
        public GuidedMissileEjection() { }

        //按弹体长度和额外净空计算弹射行程与离筒时间。
        internal void Initialize(GuidedMissileSettings settings, CombinedDefenseAssets assets, Vector3 scale)
        {
            missileLength = (assets.Tip.z - assets.Trail.z) * scale.z;
            stroke = missileLength + settings.ejectionClearance;
            releaseSpeed = settings.ejectionSpeed;
            gravity = settings.ejectionGravity;
            releaseSeconds = 2 * stroke / releaseSpeed;
            ignitionDelay = settings.ignitionDelay;
            minimumIgnitionHeight = settings.minimumIgnitionHeight;
        }

        //根据离筒高度和方向限制点火延迟。
        internal void AlignIgnition(Vector3 muzzle, Quaternion rotation, float groundHeight)
        {
            Vector3 forward = rotation * Vector3.forward;
            float tipHeight = muzzle.y + forward.y * stroke;
            float tailHeight = tipHeight - forward.y * missileLength;
            float height = Mathf.Min(tipHeight, tailHeight) - groundHeight - minimumIgnitionHeight;
            float coast = ignitionDelay;
            float verticalSpeed = forward.y * releaseSpeed;
            if (height <= 0) coast = 0;
            else if (gravity > 0)
            {
                //解出抛体首次下降到点火高度的时刻，使用八成时间保留姿态恢复距离。
                float descentSeconds = (verticalSpeed + Mathf.Sqrt(verticalSpeed * verticalSpeed + 2 * gravity * height)) / gravity;
                coast = Mathf.Min(coast, descentSeconds * 0.8f);
            }
            else if (verticalSpeed < 0) coast = Mathf.Min(coast, height / -verticalSpeed * 0.8f);
            ignitionSeconds = releaseSeconds + coast;
        }

        //计算筒内加速或离筒抛体运动的位置和速度。
        internal void Evaluate(float seconds, Vector3 muzzle, Quaternion rotation, out Vector3 tip, out Vector3 velocity)
        {
            Vector3 forward = rotation * Vector3.forward;
            if (seconds <= releaseSeconds)
            {
                float fraction = seconds / releaseSeconds;
                tip = muzzle + forward * (stroke * fraction * fraction);
                velocity = forward * (releaseSpeed * fraction);
                return;
            }
            float coast = seconds - releaseSeconds;
            tip = muzzle + forward * (stroke + releaseSpeed * coast)
                - Vector3.up * (0.5f * gravity * coast * coast);
            velocity = forward * releaseSpeed - Vector3.up * (gravity * coast);
        }

        //读写弹射和点火参数。
        public void ExposeData()
        {
            Scribe_Values.Look(ref stroke, "stroke");
            Scribe_Values.Look(ref missileLength, "missileLength");
            Scribe_Values.Look(ref releaseSpeed, "releaseSpeed");
            Scribe_Values.Look(ref gravity, "gravity");
            Scribe_Values.Look(ref releaseSeconds, "releaseSeconds");
            Scribe_Values.Look(ref ignitionSeconds, "ignitionSeconds");
            Scribe_Values.Look(ref ignitionDelay, "ignitionDelay");
            Scribe_Values.Look(ref minimumIgnitionHeight, "minimumIgnitionHeight");
        }
    }
}
