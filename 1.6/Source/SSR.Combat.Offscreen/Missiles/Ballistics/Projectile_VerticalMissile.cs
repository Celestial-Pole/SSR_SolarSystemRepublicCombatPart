using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //持久化独立垂发弹丸，命中时复用原版抛射屋顶检查和单次爆炸。
    public sealed class Projectile_VerticalMissile : Projectile_Explosive, ITurretSpatialTarget, IMissileExhaustSource
    {
        private Building_MissileSilo sourceSilo;
        private ThingDef siloDef;
        private MissileTrajectory trajectory;
        private int ageTicks;
        private bool cleared, impactResolved;
        private float clearanceHeight;
        internal Building_MissileSilo SourceSilo => sourceSilo;
        internal SiloSettings Settings => siloDef.GetModExtension<SiloSettings>();
        internal SiloAssets Assets => SiloAssets.Get(Settings);
        internal MissileTrajectory Trajectory => trajectory;
        internal float FlightSeconds => ageTicks / 60f;
        internal bool EngineIgnited => FlightSeconds >= trajectory.IgnitionSeconds;
        internal float EngineSeconds => Mathf.Max(0, FlightSeconds - trajectory.IgnitionSeconds);
        internal bool ClearOfSilo => cleared || sourceSilo == null || !sourceSilo.Spawned;
        public override int UpdateRateTicks => 1;
        SiloSettings IMissileExhaustSource.ExhaustSettings => Settings;
        Vector3 IMissileExhaustSource.ExhaustAnchor => trajectory.Anchor;
        Building_MissileSilo IMissileExhaustSource.ExhaustSilo => sourceSilo;
        GameObject IMissileExhaustSource.FlamePrefab => Assets.Flame;
        GameObject IMissileExhaustSource.SmokePrefab => Assets.Smoke;
        int IMissileExhaustSource.TrailSeed => thingIDNumber;
        float IMissileExhaustSource.FlightSeconds => FlightSeconds;
        float IMissileExhaustSource.IgnitionSeconds => trajectory.IgnitionSeconds;
        float IMissileExhaustSource.EngineSeconds => EngineSeconds;
        bool IMissileExhaustSource.EngineIgnited => EngineIgnited;

        //把垂发轨迹中的真实喷口交给共用烟焰采样器。
        void IMissileExhaustSource.ExhaustPose(float seconds, out Quaternion rotation, out Vector3 tail)
        {
            trajectory.Pose(seconds, out _, out rotation, out tail, out _);
        }
        public override Vector3 ExactPosition
        {
            get { trajectory.Evaluate(FlightSeconds, out var tip, out _); return trajectory.ProjectGround(tip); }
        }
        public override Quaternion ExactRotation
        {
            get { trajectory.Evaluate(FlightSeconds, out _, out var rotation); return rotation; }
        }

        //向通用炮塔提供弹头真实高度和地图速度，未离井的弹体不作为空中目标。
        public bool TryGetTurretTarget(out TurretTargetState state)
        {
            state = default;
            if (trajectory == null || !ClearOfSilo || impactResolved) return false;
            trajectory.Evaluate(FlightSeconds, out var tip, out _);
            trajectory.Evaluate(FlightSeconds + 1f / 60, out var next, out _);
            Vector3 velocity = (trajectory.ProjectGround(next) - trajectory.ProjectGround(tip)) * 60;
            velocity.y = (next.y - tip.y) * 60;
            state = new TurretTargetState(TurretTargetKind.Spatial, trajectory.ProjectGround(tip), tip.y, velocity);
            return true;
        }

        //在加入地图前设置完整轨迹，使出生后的任何查询都有确定的姿态。
        internal void Initialize(Building_MissileSilo silo, int slot, IntVec3 target)
        {
            sourceSilo = silo;
            siloDef = silo.def;
            clearanceHeight = silo.Assets.Layout.clearanceHeight + 0.04f;
            trajectory = new MissileTrajectory(silo, silo.Assets.Layout.slots[slot], target);
        }

        //继承发射者和武器伤害信息，但落点与计时完全由垂发轨迹确定。
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, ProjectileHitFlags.None, preventFriendlyFire, equipment, targetCoverDef);
            destination = usedTarget.Cell.ToVector3Shifted();
            ticksToImpact = Mathf.CeilToInt(trajectory.Duration * 60);
            lifetime = ticksToImpact;
        }

        //登记真实弹丸，飞行和烟迹模拟不依赖地图可见性。
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.GetComponent<MissileMapVisuals>().Add(this);
        }

        //替代原版直线积分和拦截检查，终点仍交给原版屋顶与爆炸处理。
        protected override void TickInterval(int delta)
        {
            if (impactResolved) return;
            ageTicks += delta;
            ticksToImpact = Mathf.Max(0, Mathf.CeilToInt(trajectory.Duration * 60) - ageTicks);
            trajectory.Pose(FlightSeconds, out _, out _, out _, out float tail);
            if (tail > clearanceHeight) cleared = true;
            //近边界的空中圆弧允许经过地图外侧，地面登记格保持有效，显示仍使用真实飞行位置。
            Position = ExactPosition.ToIntVec3().ClampInsideMap(Map);
            if (ticksToImpact == 0)
            {
                impactResolved = true;
                Position = destination.ToIntVec3();
                ImpactSomething();
            }
        }

        //由共享离屏相机绘制三维弹体，阻止原版再次叠加二维弹丸。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false) { }

        //释放弹体显示并停止尾焰，已经生成的烟团继续消散。
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map.GetComponent<MissileMapVisuals>().Remove(this);
            base.DeSpawn(mode);
        }

        //保存游戏弹丸信息及独立轨迹，读档不会重复发射或重复结算爆炸。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref sourceSilo, "sourceSilo");
            Scribe_Defs.Look(ref siloDef, "siloDef");
            Scribe_Deep.Look(ref trajectory, "trajectory");
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref cleared, "cleared");
            Scribe_Values.Look(ref impactResolved, "impactResolved");
            Scribe_Values.Look(ref clearanceHeight, "clearanceHeight");
        }
    }
}
