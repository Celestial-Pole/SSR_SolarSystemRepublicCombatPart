using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //以弹体中心为参考，解析计算井内弹射加速及离井后的重力抛体运动。
    internal sealed class MissileColdLaunch : IExposable
    {
        private Vector3 start, tipOffset;
        private Quaternion initialRotation;
        private float stroke, releaseSpeed, releaseSeconds, gravity;
        internal MissileLaunchAttitude Attitude;
        internal Quaternion InitialRotation => initialRotation;
        internal Vector3 TipOffset => tipOffset;

        //为读档提供弹射物理状态实例。
        public MissileColdLaunch() { }

        //按真实弹尾位置确定导向行程，使气流扰动只在整枚导弹离开舱盖后发生。
        internal MissileColdLaunch(SiloSettings settings, Vector3 center, Quaternion rotation,
            Vector3 tip, float tailZ, float clearanceHeight)
        {
            start = center;
            tipOffset = tip;
            initialRotation = rotation;
            stroke = clearanceHeight + settings.ejectionClearance - (center + rotation * new Vector3(0, 0, tailZ)).y;
            releaseSpeed = settings.ejectionSpeed;
            releaseSeconds = 2 * stroke / releaseSpeed;
            gravity = settings.ejectionGravity;
            Attitude = new MissileLaunchAttitude(settings, releaseSeconds);
        }

        //积分弹体中心的位置和速度，保持发射时的弹体姿态。
        internal void EvaluateCenter(float seconds, out Vector3 center, out Vector3 velocity, out Quaternion rotation)
        {
            float t = Mathf.Max(0, seconds);
            float height, verticalSpeed;
            if (t < releaseSeconds)
            {
                float acceleration = releaseSpeed / releaseSeconds;
                height = 0.5f * acceleration * t * t;
                verticalSpeed = acceleration * t;
            }
            else
            {
                t -= releaseSeconds;
                height = stroke + releaseSpeed * t - 0.5f * gravity * t * t;
                verticalSpeed = releaseSpeed - gravity * t;
            }
            Attitude.ColdPose(seconds, out var drift, out var driftVelocity);
            rotation = initialRotation;
            center = start + Vector3.up * height + drift;
            velocity = Vector3.up * verticalSpeed + driftVelocity;
        }

        //保存质心、导向行程及随机扰动，飞行状态不依赖显示对象是否存在。
        public void ExposeData()
        {
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref tipOffset, "tipOffset");
            Scribe_Values.Look(ref initialRotation, "initialRotation");
            Scribe_Values.Look(ref stroke, "stroke");
            Scribe_Values.Look(ref releaseSpeed, "releaseSpeed");
            Scribe_Values.Look(ref releaseSeconds, "releaseSeconds");
            Scribe_Values.Look(ref gravity, "gravity");
            Scribe_Deep.Look(ref Attitude, "attitude");
        }
    }
}
