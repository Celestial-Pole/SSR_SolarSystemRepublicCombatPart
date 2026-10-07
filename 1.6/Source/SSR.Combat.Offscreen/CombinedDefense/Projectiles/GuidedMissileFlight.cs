using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //推进导弹冷发射和点火后的制导运动。
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

        //供存档系统创建飞行状态。
        public GuidedMissileFlight() { }

        //按发射挂点和弹体尺寸初始化冷发射状态。
        internal void Initialize(Vector3 tip, Quaternion rotation, Vector3 scale, Vector3 anchor,
            GuidedMissileSettings settings, CombinedDefenseAssets assets)
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

        //出筒前同步发射器姿态和点火时刻。
        internal void AlignEjection(Vector3 muzzle, Quaternion rotation)
        {
            LaunchTip = muzzle;
            LaunchRotation = rotation;
            ejection.AlignIgnition(muzzle, rotation, Anchor.y);
        }

        //分段推进冷发射和动力飞行，按点火时刻划分步长。
        internal void Tick(GuidedMissileSettings settings, TurretTargetState? target, float seconds)
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

        //按目标提前量转向，以配置的加速度和转速推进飞行。
        private void TickPowered(GuidedMissileSettings settings, TurretTargetState? target, float seconds)
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

        //将目标地面坐标和高度转换到模型采集空间。
        internal Vector3 TargetPoint(TurretTargetState state)
        {
            return new Vector3(state.GroundPosition.x, Anchor.y + state.Height,
                Anchor.z + (state.GroundPosition.z - Anchor.z) / GroundProjection);
        }

        //将模型采集坐标投影到地图地面。
        internal Vector3 Ground(Vector3 point)
        {
            return new Vector3(point.x, 0, Anchor.z + (point.z - Anchor.z) * GroundProjection);
        }

        //插值弹头姿态，计算模型根节点和尾喷口位置。
        internal void Pose(float fraction, CombinedDefenseAssets assets, out Vector3 root, out Quaternion rotation, out Vector3 trail)
        {
            var tip = Vector3.Lerp(PreviousTip, Tip, fraction);
            rotation = Quaternion.Slerp(PreviousRotation, Rotation, fraction);
            root = tip - rotation * Vector3.Scale(assets.Tip, Scale);
            trail = root + rotation * Vector3.Scale(assets.Trail, Scale);
        }

        //读写飞行姿态、速度和冷发射状态。
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
