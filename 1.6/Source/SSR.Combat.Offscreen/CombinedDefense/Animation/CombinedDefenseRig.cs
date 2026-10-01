using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //绑定八个导弹库存、各自炮口和独立雷达，保留预制体中的真实机械位置。
    internal sealed class CombinedDefenseRig
    {
        internal readonly GameObject Model;
        internal readonly Transform[] Payloads, FirePoints;
        private readonly Transform[] radars;
        private readonly Quaternion[] radarRest;
        private readonly CombinedAirDefenseSettings settings;

        //按 XML 挂点绑定整座武器，不在游戏中生成轴承或改变模型尺寸。
        internal CombinedDefenseRig(GameObject model, CombinedAirDefenseSettings settings)
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
            radars = new Transform[settings.radars.Count];
            radarRest = new Quaternion[radars.Length];
            for (int i = 0; i < radars.Length; i++)
            {
                radars[i] = Require(settings.radars[i].path);
                radarRest[i] = radars[i].localRotation;
            }
        }

        //按逻辑刻旋转雷达，独立角度不受武器俯仰或转管动画覆盖。
        internal void TickRadars(List<float> angles, bool powered)
        {
            for (int i = 0; i < radars.Length; i++)
            {
                if (powered) angles[i] = Mathf.Repeat(angles[i] + settings.radars[i].degreesPerSecond / 60f, 360);
                radars[i].localRotation = radarRest[i] * Quaternion.AngleAxis(angles[i], settings.radars[i].axis);
            }
        }

        //隐藏已经发射的弹体，冷却完成后恢复原弹位，不隐藏发射筒。
        internal void ShowPayloads(List<int> cooldowns)
        {
            for (int i = 0; i < Payloads.Length; i++) Payloads[i].gameObject.SetActive(cooldowns[i] == 0);
        }

        //读取必需挂点，路径错误直接报告，不回退到模型中心。
        private Transform Require(string path)
        {
            var result = Model.transform.Find(path);
            if (!result) throw new InvalidOperationException("弹炮合一预制体缺少挂点：" + path);
            return result;
        }
    }
}
