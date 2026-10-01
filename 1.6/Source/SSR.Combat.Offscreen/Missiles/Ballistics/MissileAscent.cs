using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //以弹体中心连接冷弹射、推力建立和竖直加速，保持发射时的弹体姿态。
    internal sealed class MissileAscent : IExposable
    {
        private MissileColdLaunch cold;
        private Vector3 ignitionCenter, ignitionVelocity;
        private float ignitionDelay, poweredSeconds, gravity, acceleration, rampSeconds;
        private float rampHeight, rampSpeed, endCenterHeight;
        internal float IgnitionDelay => ignitionDelay;
        internal float Duration => ignitionDelay + poweredSeconds;
        internal float EndSpeed => rampSpeed + acceleration * (poweredSeconds - rampSeconds);
        internal Vector3 EndPosition => new Vector3(
            ignitionCenter.x + ignitionVelocity.x * poweredSeconds * 0.5f, endCenterHeight,
            ignitionCenter.z + ignitionVelocity.z * poweredSeconds * 0.5f) + cold.InitialRotation * cold.TipOffset;

        //提供用于恢复飞行系数的存档实例。
        public MissileAscent() { }

        //由出井余速、推力和终点高度解出上升时间，接续冷发射的平移惯性。
        internal MissileAscent(SiloSettings settings, MissileColdLaunch launch)
        {
            cold = launch;
            ignitionDelay = settings.ignitionDelay;
            gravity = settings.ejectionGravity;
            acceleration = settings.ignitionAcceleration;
            rampSeconds = settings.ignitionRampSeconds;
            cold.EvaluateCenter(ignitionDelay, out ignitionCenter, out ignitionVelocity, out _);
            endCenterHeight = settings.ascentHeight - (cold.InitialRotation * cold.TipOffset).y;
            //发动机推力从零线性建立；对加速度积分，点火前后位置和速度相同。
            rampHeight = ignitionCenter.y + ignitionVelocity.y * rampSeconds
                - 0.5f * gravity * rampSeconds * rampSeconds
                + (acceleration + gravity) * rampSeconds * rampSeconds / 6f;
            rampSpeed = ignitionVelocity.y + (acceleration - gravity) * rampSeconds * 0.5f;
            float distance = endCenterHeight - rampHeight;
            if (distance <= 0 || rampSpeed <= 0)
                throw new InvalidOperationException("导弹动力抬升高度不足或推力建立后仍在回落，请检查冷发射和点火参数。");
            float endSpeed = Mathf.Sqrt(rampSpeed * rampSpeed + 2 * acceleration * distance);
            poweredSeconds = rampSeconds + 2 * distance / (rampSpeed + endSpeed);
        }

        //在冷弹射阶段使用质心抛体轨迹，点火后连续加速并消除离井侧向漂移。
        internal void Evaluate(float seconds, out Vector3 tip, out Quaternion rotation)
        {
            Vector3 center;
            if (seconds <= ignitionDelay)
            {
                cold.EvaluateCenter(seconds, out center, out _, out rotation);
            }
            else
            {
                float t = Mathf.Clamp(seconds - ignitionDelay, 0, poweredSeconds);
                //制导平滑消除侧向余速，抬升出口的切线竖直向上。
                center = ignitionCenter + new Vector3(ignitionVelocity.x, 0, ignitionVelocity.z)
                    * (t - t * t / (2 * poweredSeconds));
                center.y = HeightAt(t);
                rotation = cold.InitialRotation;
            }
            tip = center + rotation * cold.TipOffset;
        }

        //解析积分推力建立段及恒定净加速度段，转弯前不施加任何减速曲线。
        private float HeightAt(float t)
        {
            if (t <= rampSeconds)
                return ignitionCenter.y + ignitionVelocity.y * t - 0.5f * gravity * t * t
                    + (acceleration + gravity) * t * t * t / (6 * rampSeconds);
            t -= rampSeconds;
            return rampHeight + rampSpeed * t + 0.5f * acceleration * t * t;
        }

        //保存弹射状态和发动机积分系数，读档后沿原有时钟继续飞行。
        public void ExposeData()
        {
            Scribe_Deep.Look(ref cold, "cold");
            Scribe_Values.Look(ref ignitionCenter, "ignitionCenter");
            Scribe_Values.Look(ref ignitionVelocity, "ignitionVelocity");
            Scribe_Values.Look(ref ignitionDelay, "ignitionDelay");
            Scribe_Values.Look(ref poweredSeconds, "poweredSeconds");
            Scribe_Values.Look(ref gravity, "gravity");
            Scribe_Values.Look(ref acceleration, "acceleration");
            Scribe_Values.Look(ref rampSeconds, "rampSeconds");
            Scribe_Values.Look(ref rampHeight, "rampHeight");
            Scribe_Values.Look(ref rampSpeed, "rampSpeed");
            Scribe_Values.Look(ref endCenterHeight, "endCenterHeight");
        }
    }
}
