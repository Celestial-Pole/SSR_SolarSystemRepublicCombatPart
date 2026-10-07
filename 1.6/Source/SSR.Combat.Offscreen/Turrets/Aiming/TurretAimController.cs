using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理炮塔瞄准角度、转速和回正状态。
    internal sealed class TurretAimController : IExposable
    {
        private readonly Building_ConfigurableTurret owner;
        private TurretAimRig rig;
        private float yaw, pitch, yawVelocity, pitchVelocity;
        private int idleTicks;
        internal float Yaw => yaw;
        internal float Pitch => Mathf.DeltaAngle(0, pitch);

        //绑定所属建筑。
        public TurretAimController(Building_ConfigurableTurret owner) { this.owner = owner; }

        //更新瞄准转轴，失去目标后延迟回正。
        internal void Tick(bool powered)
        {
            Prepare();
            if (!powered) { yawVelocity = pitchVelocity = 0; return; }
            var settings = owner.Settings;
            float desiredYaw = yaw, desiredPitch = pitch;
            if (owner.TryReadTarget(owner.CurrentTarget, out var state))
            {
                idleTicks = 0;
                TurretAimSolver.Solve(rig, settings, rig.TargetPoint(state), yaw, pitch, out desiredYaw, out desiredPitch);
            }
            else if (settings.returnToIdle && ++idleTicks >= settings.idleDelayTicks)
            { desiredYaw = settings.idleYaw; desiredPitch = settings.idlePitch; }
            yaw = TurretAngleLimits.Advance(yaw, desiredYaw, ref yawVelocity, settings.yawRotationSpeed,
                settings.yawAcceleration, settings.yawRotationRange);
            pitch = TurretAngleLimits.Advance(pitch, desiredPitch, ref pitchVelocity, settings.pitchRotationSpeed,
                settings.pitchAcceleration, settings.pitchRotationRange);
            rig.Apply(yaw, pitch);
        }

        //判断机械转角范围能否覆盖目标。
        internal bool CanReach(TurretTargetState state)
        {
            Prepare();
            return TurretAimSolver.Solve(rig, owner.Settings, rig.TargetPoint(state), yaw, pitch, out _, out _);
        }

        //检查两轴角度误差和炮口锥角。
        internal bool Aligned(TurretTargetState state)
        {
            Prepare();
            var settings = owner.Settings;
            Vector3 point = rig.TargetPoint(state);
            if (!TurretAimSolver.Solve(rig, settings, point, yaw, pitch, out float wantedYaw, out float wantedPitch)) return false;
            return Mathf.Abs(TurretAngleLimits.Delta(yaw, wantedYaw, settings.yawRotationRange)) <= settings.yawAimTolerance
                && Mathf.Abs(TurretAngleLimits.Delta(pitch, wantedPitch, settings.pitchRotationRange)) <= settings.pitchAimTolerance
                && Vector3.Angle(rig.Forward, point - rig.Muzzle.position) <= settings.aimConeTolerance;
        }

        //返回炮口的地图投影位置。
        internal Vector3 MuzzleMapPosition { get { Prepare(); return rig.MuzzleMapPosition; } }

        //返回目标在模型采集空间中的坐标。
        internal Vector3 TargetPoint(TurretTargetState target)
        {
            Prepare();
            return rig.TargetPoint(target);
        }

        //采集炮口姿态和目标高度。
        internal TurretShotPose CaptureShot(LocalTargetInfo target)
        {
            Prepare();
            if (!owner.TryReadTarget(target, out var state))
                throw new InvalidOperationException("发射目标无法读取：" + owner.def.defName);
            return rig.CaptureShot(state);
        }

        //绑定当前模型并同步瞄准姿态。
        internal void Prepare()
        {
            var comp = owner.GetComp<OffscreenTurretComp>();
            if (comp == null) throw new InvalidOperationException("炮塔缺少离屏模型组件：" + owner.def.defName);
            var model = comp.CurrentUnityObject;
            if (!model) throw new InvalidOperationException("炮塔预制体无法装载：" + owner.def.defName);
            if (rig == null || rig.Model != model.ownGameObject) rig = new TurretAimRig(owner, comp, model.ownGameObject);
            model.expirationTick = 6000;
            rig.Synchronize();
            rig.Apply(yaw, pitch);
        }

        //读写转角、转速和回正计时。
        public void ExposeData()
        {
            Scribe_Values.Look(ref yaw, "yaw");
            Scribe_Values.Look(ref pitch, "pitch");
            Scribe_Values.Look(ref yawVelocity, "yawVelocity");
            Scribe_Values.Look(ref pitchVelocity, "pitchVelocity");
            Scribe_Values.Look(ref idleTicks, "idleTicks");
        }
    }
}
