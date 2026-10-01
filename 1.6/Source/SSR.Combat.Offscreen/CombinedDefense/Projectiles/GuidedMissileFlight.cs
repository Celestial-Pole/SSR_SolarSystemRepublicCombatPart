using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //协调沿发射器瞄准轴的冷弹射、离筒滑行和点火后的三维制导飞行。
    internal sealed class GuidedMissileFlight : IExposable
    {
        internal Vector3 Tip, PreviousTip, Anchor, Scale, LaunchTip;
        internal Quaternion Rotation, PreviousRotation, LaunchRotation;
        private GuidedMissileEjection ejection = new GuidedMissileEjection();
        private Vector3 velocity;
        private float elapsedSeconds;
        internal bool Clear => elapsedSeconds >= ejection.ReleaseSeconds;
        internal bool Ignited => elapsedSeconds >= ejection.IgnitionSeconds;
        internal float IgnitionSeconds => ejection.IgnitionSeconds;
        private static float GroundProjection => (TurretCaptureProfile.Direction * Vector3.up).z;

        //为深度存档提供独立运动状态实例。
        public GuidedMissileFlight() { }

        //以发射挂点作为弹头参考，按库存弹体尺寸建立从静止开始的冷弹射。
        internal void Initialize(Vector3 tip, Quaternion rotation, Vector3 scale, Vector3 anchor,
            CombinedAirDefenseSettings settings, CombinedDefenseAssets assets)
        {
            Tip = PreviousTip = LaunchTip = tip;
            Rotation = PreviousRotation = LaunchRotation = rotation;
            Scale = scale;
            Anchor = anchor;
            velocity = Vector3.zero;
            elapsedSeconds = 0;
            ejection.Initialize(settings, assets, scale);
            AlignEjection(tip, rotation);
        }

        //筒内弹体持续对齐真实发射器，离筒时由调用方停止更新并保留出口姿态。
        internal void AlignEjection(Vector3 muzzle, Quaternion rotation)
        {
            LaunchTip = muzzle;
            LaunchRotation = rotation;
            ejection.AlignIgnition(muzzle, rotation, Anchor.y);
        }

        //按实际时间划分冷弹射和点火阶段，跨越点火刻时只对剩余时间施加发动机推力。
        internal void Tick(CombinedAirDefenseSettings settings, TurretTargetState? target, float seconds)
        {
            PreviousTip = Tip;
            PreviousRotation = Rotation;
            float previousSeconds = elapsedSeconds;
            elapsedSeconds = seconds;
            if (previousSeconds < ejection.IgnitionSeconds)
            {
                ejection.Evaluate(Mathf.Min(seconds, ejection.IgnitionSeconds), LaunchTip, LaunchRotation,
                    out Tip, out velocity);
                Rotation = LaunchRotation;
            }
            float poweredSeconds = seconds - Mathf.Max(previousSeconds, ejection.IgnitionSeconds);
            if (poweredSeconds > 0) TickPowered(settings, target, poweredSeconds);
        }

        //点火后接续滑行速度，以有限转速对准目标，并保持位置和速度连续。
        private void TickPowered(CombinedAirDefenseSettings settings, TurretTargetState? target, float seconds)
        {
            if (target.HasValue)
            {
                var state = target.Value;
                Vector3 point = TargetPoint(state);
                float lead = Mathf.Min(2, Vector3.Distance(point, Tip) / settings.maximumSpeed);
                point += new Vector3(state.Velocity.x, state.Velocity.y, state.Velocity.z / GroundProjection) * lead;
                Vector3 desired = point - Tip;
                if (desired.sqrMagnitude > 0.0001f)
                    Rotation = Quaternion.RotateTowards(Rotation, Quaternion.LookRotation(desired, Rotation * Vector3.up),
                        settings.turnDegreesPerSecond * seconds);
            }
            float speed = Mathf.MoveTowards(velocity.magnitude, settings.maximumSpeed, settings.acceleration * seconds);
            velocity = Vector3.RotateTowards(velocity.normalized, Rotation * Vector3.forward,
                settings.turnDegreesPerSecond * Mathf.Deg2Rad * seconds, 0) * speed;
            Tip += velocity * seconds;
        }

        //把目标地面坐标与真实高度转换成模型采集空间。
        internal Vector3 TargetPoint(TurretTargetState state)
        {
            return new Vector3(state.GroundPosition.x, Anchor.y + state.Height,
                Anchor.z + (state.GroundPosition.z - Anchor.z) / GroundProjection);
        }

        //将弹头地面位置还原为地图格坐标，高度独立传递给防空系统。
        internal Vector3 Ground(Vector3 point)
        {
            return new Vector3(point.x, 0, Anchor.z + (point.z - Anchor.z) * GroundProjection);
        }

        //采样上一刻到当前刻的真实喷口，为连续弯曲尾迹提供细分位置。
        internal void Pose(float fraction, CombinedDefenseAssets assets, out Vector3 root, out Quaternion rotation, out Vector3 trail)
        {
            var tip = Vector3.Lerp(PreviousTip, Tip, fraction);
            rotation = Quaternion.Slerp(PreviousRotation, Rotation, fraction);
            root = tip - rotation * Vector3.Scale(assets.Tip, Scale);
            trail = root + rotation * Vector3.Scale(assets.Trail, Scale);
        }

        //保存弹头位置、姿态、速度和冷发射阶段，读档继续原有航迹。
        public void ExposeData()
        {
            Scribe_Values.Look(ref Tip, "tip");
            Scribe_Values.Look(ref PreviousTip, "previousTip");
            Scribe_Values.Look(ref Anchor, "anchor");
            Scribe_Values.Look(ref Scale, "scale");
            Scribe_Values.Look(ref LaunchTip, "launchTip");
            Scribe_Values.Look(ref Rotation, "rotation");
            Scribe_Values.Look(ref PreviousRotation, "previousRotation");
            Scribe_Values.Look(ref LaunchRotation, "launchRotation");
            Scribe_Values.Look(ref velocity, "velocity");
            Scribe_Values.Look(ref elapsedSeconds, "elapsedSeconds");
            Scribe_Deep.Look(ref ejection, "ejection");
        }
    }
}
