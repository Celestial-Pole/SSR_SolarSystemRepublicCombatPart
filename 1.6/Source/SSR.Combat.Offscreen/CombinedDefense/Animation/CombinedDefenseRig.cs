using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //绑定导弹俯仰、弹位和雷达节点。
    internal sealed class CombinedDefenseRig
    {
        internal readonly GameObject Model;
        internal readonly Transform[] Payloads, FirePoints;
        private readonly Transform[] radars;
        private readonly Quaternion[] radarRest;
        private readonly Transform missilePitch, missileAimPoint;
        private readonly Quaternion missilePitchRest;
        private readonly CombinedAirDefenseSettings settings;

        //按 XML 路径绑定机械挂点。
        internal CombinedDefenseRig(GameObject model, CombinedAirDefenseSettings settings)
        {
            Model = model;
            this.settings = settings;
            missilePitch = Require(settings.missilePitchPath);
            missileAimPoint = Require(settings.missileAimPointPath);
            missilePitchRest = missilePitch.localRotation;
            Payloads = new Transform[settings.missileSlots.Count];
            FirePoints = new Transform[Payloads.Length];
            for (int i = 0; i < Payloads.Length; i++)
            {
                Payloads[i] = Require(settings.missileSlots[i] + "/Missile");
                FirePoints[i] = Require(settings.missileSlots[i] + "/FirePoint");
            }
            radars = new Transform[settings.radars.Count];
            radarRest = new Quaternion[radars.Length];
            for (int i = 0; i < radars.Length; i++)
            {
                radars[i] = Require(settings.radars[i].path);
                radarRest[i] = radars[i].localRotation;
            }
        }

        //设置导弹架俯仰。
        internal void ApplyMissilePitch(float pitch)
        {
            missilePitch.localRotation = missilePitchRest * Quaternion.AngleAxis(pitch, settings.missilePitchAxis.normalized);
        }

        //求解当前底座方位下的导弹俯仰。
        internal float SolveMissilePitch(Vector3 target, float current, int iterations)
        {
            float pitch = current;
            try
            {
                for (int i = 0; i < iterations; i++)
                {
                    ApplyMissilePitch(pitch);
                    Vector3 axis = missilePitch.TransformDirection(settings.missilePitchAxis).normalized;
                    Vector3 forward = Vector3.ProjectOnPlane(missileAimPoint.forward, axis);
                    Vector3 desired = Vector3.ProjectOnPlane(target - missileAimPoint.position, axis);
                    if (desired.sqrMagnitude < 0.000001f) break;
                    float delta = Vector3.SignedAngle(forward, desired, axis);
                    pitch = TurretAngleLimits.Clamp(pitch + delta, settings.missilePitchRange);
                    if (Mathf.Abs(delta) <= 0.05f) break;
                }
                return pitch;
            }
            finally { ApplyMissilePitch(current); }
        }

        //检查目标是否进入导弹发射锥。
        internal bool MissilesAligned(Vector3 target, float tolerance)
        {
            return Vector3.Angle(missileAimPoint.forward, target - missileAimPoint.position) <= tolerance;
        }

        //按逻辑刻更新雷达旋转。
        internal void TickRadars(List<float> angles, bool powered)
        {
            for (int i = 0; i < radars.Length; i++)
            {
                if (powered) angles[i] = Mathf.Repeat(angles[i] + settings.radars[i].degreesPerSecond / 60f, 360);
                radars[i].localRotation = radarRest[i] * Quaternion.AngleAxis(angles[i], settings.radars[i].axis);
            }
        }

        //按库存设置各弹位的弹体可见性。
        internal void ShowPayloads(List<int> cooldowns)
        {
            for (int i = 0; i < Payloads.Length; i++) Payloads[i].gameObject.SetActive(cooldowns[i] == 0);
        }

        //查找挂点，缺失时报告路径。
        private Transform Require(string path)
        {
            var result = Model.transform.Find(path);
            if (!result) throw new InvalidOperationException("弹炮合一预制体缺少挂点：" + path);
            return result;
        }
    }
}
