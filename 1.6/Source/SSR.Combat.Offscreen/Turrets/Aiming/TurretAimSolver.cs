using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //在模型实际转轴上迭代求解目标角度，支持不同型号的轴向、炮口偏移和机械限位。
    internal static class TurretAimSolver
    {
        //临时移动逻辑挂点求解目标角度，结束后恢复真实姿态，渲染不会看见求解中间态。
        internal static bool Solve(TurretAimRig rig, TurretAimSettings settings, TurretTargetState state,
            float currentYaw, float currentPitch, out float yaw, out float pitch)
        {
            Vector3 target = rig.TargetPoint(state);
            bool ground = state.Kind == TurretTargetKind.Ground;
            yaw = currentYaw;
            pitch = ground ? 0 : currentPitch;
            try
            {
                for (int i = 0; i < settings.solverIterations; i++)
                {
                    rig.Apply(yaw, pitch);
                    if (settings.yawRotationSpeed > 0)
                        yaw = Adjust(rig, rig.Yaw, settings.yawRotationAixe, target,
                            ground ? rig.Yaw.position : rig.Muzzle.position, yaw, settings.yawRotationRange);
                    rig.Apply(yaw, pitch);
                    if (!ground && settings.pitchRotationSpeed > 0)
                        pitch = Adjust(rig, rig.Pitch, settings.pitchRotationAixe, target,
                            rig.Muzzle.position, pitch, settings.pitchRotationRange);
                    rig.Apply(yaw, pitch);
                    if (AimError(rig, target, ground) <= 0.05f) return true;
                }
                return AimError(rig, target, ground) <= settings.aimConeTolerance;
            }
            finally { rig.Apply(currentYaw, currentPitch); }
        }

        //对地只比较水平朝向，以旋转轴为起点，避免近目标落在长炮管后方。
        internal static float AimError(TurretAimRig rig, Vector3 target, bool ground)
        {
            if (!ground) return Vector3.Angle(rig.Forward, target - rig.Muzzle.position);
            return Vector3.Angle(Vector3.ProjectOnPlane(rig.Forward, Vector3.up),
                Vector3.ProjectOnPlane(target - rig.Yaw.position, Vector3.up));
        }

        //投影炮口前向和目标方向到转轴法面，求该轴下一次允许的角度修正。
        private static float Adjust(TurretAimRig rig, Transform joint, Vector3 axis, Vector3 target,
            Vector3 origin, float angle, Vector2 range)
        {
            Vector3 worldAxis = joint.TransformDirection(axis).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(rig.Forward, worldAxis);
            Vector3 desired = Vector3.ProjectOnPlane(target - origin, worldAxis);
            if (forward.sqrMagnitude < 0.000001f || desired.sqrMagnitude < 0.000001f) return angle;
            return TurretAngleLimits.Clamp(angle + Vector3.SignedAngle(forward, desired, worldAxis), range);
        }
    }
}
