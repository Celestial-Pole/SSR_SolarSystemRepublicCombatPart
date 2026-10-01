using FS_SSR;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //沿用核心包的预制体装载和动画组件，将炮塔绘制入口交给离屏系统。
    public sealed class OffscreenPrefabProperties : TCP_SinglePrefabDrawer
    {
        //指定模型姿态根节点，实例化时归零旋转，由共享正交相机统一决定观察角度。
        public string modelRootPath = "Transform_Y/Root";
        public string recoilTransformPath;
        public string recoilClipName;
        public string barrelSpinTransformPath;
        public float barrelSpinDegreesPerSecond = 6000;
        //枪管数量大于零时按实际逐发间隔同步转速；为零时使用独立转速。
        public int barrelCount;
        public Vector3 barrelSpinAxis = Vector3.right;
        public float barrelFirePhaseDegrees;
        public int barrelSpinUpTicks = 18;
        public int barrelSpinDownTicks = 18;

        //为 XML 炮塔配置专用的离屏组件。
        public OffscreenPrefabProperties() { compClass = typeof(OffscreenTurretComp); }

        //检查转轮配置，阻止无效轴向、枪管数量和启停时间进入机械计算。
        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef)) yield return error;
            if (barrelCount < 0 || barrelSpinUpTicks < 1 || barrelSpinDownTicks < 1
                || barrelSpinDegreesPerSecond <= 0 || barrelSpinAxis.sqrMagnitude < 0.0001f)
                yield return "炮塔转轮配置无效：" + parentDef.defName;
            if (barrelCount > 0 && string.IsNullOrEmpty(barrelSpinTransformPath))
                yield return "同步转轮未指定模型节点：" + parentDef.defName;
        }

        //指定地图上的纹理绘制入口，避免核心包直接显示模型。
        public override void ResolveReferences(ThingDef parentDef)
        {
            parentDef.graphicData.graphicClass = typeof(OffscreenTurretGraphic);
            parentDef.graphic = null;
            parentDef.drawerType = DrawerType.RealtimeOnly;
        }
    }
}
