using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //定义每个炮塔的模型挂点、转向性能和开火精度，数值由建筑 XML 配置。
    public sealed class TurretAimSettings : DefModExtension
    {
        public string rootTransform, yawPath, pitchPath, shootingOrigine;
        public Vector3 yawRotationAixe = Vector3.up, pitchRotationAixe = Vector3.right;
        public Vector3 shootingOrigineForward = Vector3.forward;
        public Vector2 yawRotationRange = Vector2.zero;
        public Vector2 pitchRotationRange = new Vector2(350, 85);
        public float yawRotationSpeed = 180, pitchRotationSpeed = 120;
        public float yawAcceleration, pitchAcceleration;
        public float yawAimTolerance = 1, pitchAimTolerance = 1, aimConeTolerance = 1.5f;
        public int solverIterations = 12;
        public bool returnToIdle = true, allowManualTarget = true;
        public int idleDelayTicks = 180;
        public float idleYaw, idlePitch;
        public SoundDef shootingSoundDef;
        public TurretTargetingDef targeting;

        //在定义加载阶段报告缺失挂点和非法参数，避免生成建筑后才产生空引用。
        public override IEnumerable<string> ConfigErrors()
        {
            if (string.IsNullOrWhiteSpace(rootTransform) || string.IsNullOrWhiteSpace(yawPath)
                || string.IsNullOrWhiteSpace(pitchPath) || string.IsNullOrWhiteSpace(shootingOrigine))
                yield return "炮塔必须配置根节点、旋转节点、俯仰节点和炮口路径。";
            if (targeting == null) yield return "炮塔必须指定 targeting 目标配置。";
            if (yawRotationAixe.sqrMagnitude < 0.001f || pitchRotationAixe.sqrMagnitude < 0.001f
                || shootingOrigineForward.sqrMagnitude < 0.001f) yield return "炮塔转轴和炮口前向不能为零。";
            if (yawRotationSpeed < 0 || pitchRotationSpeed < 0 || yawAcceleration < 0 || pitchAcceleration < 0)
                yield return "炮塔速度和加速度不能为负数。";
            if (yawAimTolerance <= 0 || pitchAimTolerance <= 0 || aimConeTolerance <= 0
                || yawAimTolerance > 30 || pitchAimTolerance > 30 || aimConeTolerance > 30)
                yield return "炮塔开火角度容差必须大于零且不超过三十度。";
            if (solverIterations < 1 || solverIterations > 32 || idleDelayTicks < 0)
                yield return "炮塔求解次数必须为一至三十二次，回正延迟不能为负数。";
        }
    }
}
