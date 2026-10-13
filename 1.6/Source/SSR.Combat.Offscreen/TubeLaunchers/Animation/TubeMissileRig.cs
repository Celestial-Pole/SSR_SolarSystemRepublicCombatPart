using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //绑定筒内弹体、发射挂点和舱盖。
    internal sealed class TubeMissileRig
    {
        internal readonly GameObject Model;
        internal readonly Transform[] Payloads, FirePoints;
        private readonly Transform[] doors;
        private readonly Quaternion[] doorRest;
        private readonly TubeLauncherSettings settings;

        //按配置路径绑定预制体节点。
        internal TubeMissileRig(GameObject model, TubeLauncherSettings settings)
        {
            Model = model;
            this.settings = settings;
            Payloads = new Transform[settings.missileSlots.Count];
            FirePoints = new Transform[Payloads.Length];
            for (int i = 0; i < Payloads.Length; i++)
            {
                Payloads[i] = Require(settings.missileSlots[i] + "/Missile");
                FirePoints[i] = Require(settings.missileSlots[i] + "/FirePoint");
            }
            doors = new Transform[settings.doors.Count];
            doorRest = new Quaternion[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i] = Require(settings.doors[i].path);
                doorRest[i] = doors[i].localRotation;
            }
        }

        //同步库存弹体和舱盖姿态。
        internal void Apply(List<int> cooldowns, float opening)
        {
            for (int i = 0; i < Payloads.Length; i++) Payloads[i].gameObject.SetActive(cooldowns[i] == 0);
            for (int i = 0; i < doors.Length; i++)
                doors[i].localRotation = doorRest[i] * Quaternion.AngleAxis(settings.doors[i].openAngle * opening,
                    settings.doors[i].axis.normalized);
        }

        //查找挂点，缺失时报告路径。
        private Transform Require(string path)
        {
            var result = Model.transform.Find(path);
            if (!result) throw new InvalidOperationException("筒式发射器缺少挂点：" + path);
            return result;
        }
    }
}
