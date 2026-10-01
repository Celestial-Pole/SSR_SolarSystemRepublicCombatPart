using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //在模型实际转轴上迭代求解目标角度，支持不同型号的轴向、炮口偏移和机械限位。
    internal static class TurretAimSolver
    {
        //临时移动逻辑挂点求解目标角度，结束后恢复真实姿态，渲染不会看见求解中间态。
        internal static bool Solve(TurretAimRig rig, TurretAimSettings settings, Vector3 target,
            float currentYaw, float currentPitch, out float yaw, out float pitch)
        {
            yaw = currentYaw;
            pitch = currentPitch;
            try
            {
                for (int i = 0; i < settings.solverIterations; i++)
                {
                    rig.Apply(yaw, pitch);
                    if (settings.yawRotationSpeed > 0)
                        yaw = Adjust(rig, rig.Yaw, settings.yawRotationAixe, target, yaw, settings.yawRotationRange);
                    rig.Apply(yaw, pitch);
                    if (settings.pitchRotationSpeed > 0)
                        pitch = Adjust(rig, rig.Pitch, settings.pitchRotationAixe, target, pitch, settings.pitchRotationRange);
                    rig.Apply(yaw, pitch);
                    if (Vector3.Angle(rig.Forward, target - rig.Muzzle.position) <= 0.05f) return true;
                }
                return Vector3.Angle(rig.Forward, target - rig.Muzzle.position) <= settings.aimConeTolerance;
            }
            finally { rig.Apply(currentYaw, currentPitch); }
        }

        //投影炮口前向和目标方向到转轴法面，求该轴下一次允许的角度修正。
        private static float Adjust(TurretAimRig rig, Transform joint, Vector3 axis, Vector3 target, float angle, Vector2 range)
        {
            Vector3 worldAxis = joint.TransformDirection(axis).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(rig.Forward, worldAxis);
            Vector3 desired = Vector3.ProjectOnPlane(target - rig.Muzzle.position, worldAxis);
            if (forward.sqrMagnitude < 0.000001f || desired.sqrMagnitude < 0.000001f) return angle;
            return TurretAngleLimits.Clamp(angle + Vector3.SignedAngle(forward, desired, worldAxis), range);
        }
    }
}
