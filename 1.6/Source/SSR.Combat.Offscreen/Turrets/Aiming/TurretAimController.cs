using System;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理每座炮塔的逻辑角度、伺服速度和回正状态，完全按游戏刻运行并保存。
    internal sealed class TurretAimController : IExposable
    {
        private readonly Building_ConfigurableTurret owner;
        private TurretAimRig rig;
        private float yaw, pitch, yawVelocity, pitchVelocity;
        private int idleTicks;
        internal float Yaw => yaw;
        internal float Pitch => Mathf.DeltaAngle(0, pitch);

        //绑定建筑，使存档中的角度与运行时模型实例分离。
        public TurretAimController(Building_ConfigurableTurret owner) { this.owner = owner; }

        //按当前目标求解并推进转轴，丢失目标后按配置延迟回正。
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

        //按机械范围判断目标能否被炮口指向，供搜索器排除无法瞄准的目标。
        internal bool CanReach(TurretTargetState state)
        {
            Prepare();
            return TurretAimSolver.Solve(rig, owner.Settings, rig.TargetPoint(state), yaw, pitch, out _, out _);
        }

        //同时检查两轴误差与炮口锥角，连续射击期间也采用同一开火条件。
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

        //取得已经过地图投影的炮口出生位置。
        internal Vector3 MuzzleMapPosition { get { Prepare(); return rig.MuzzleMapPosition; } }

        //读取当前实际炮口姿态和目标高度，供弹丸建立独立三维航迹。
        internal TurretShotPose CaptureShot(LocalTargetInfo target)
        {
            Prepare();
            if (!owner.TryReadTarget(target, out var state))
                throw new InvalidOperationException("发射目标无法读取：" + owner.def.defName);
            return rig.CaptureShot(state);
        }

        //实例更换后重新绑定挂点，每次使用都同步逻辑姿态，避免镜头可见性控制瞄准。
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

        //保存转角、转速和回正计时，读档保持暂停前的转动阶段。
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
