using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理制导弹丸的飞行、碰撞和烟焰接口。
    public sealed class Projectile_GuidedDefenseMissile : Projectile_Explosive, ITurretSpatialTarget, IMissileExhaustSource
    {
        private ThingDef sourceDef;
        private Building_ConfigurableTurret source;
        private GuidedMissileFlight flight = new GuidedMissileFlight();
        private int ageTicks, launchSlot;
        private Vector3 lastGroundTarget;
        private bool hasGroundTarget;
        internal GuidedMissileFlight Flight => flight;
        internal Building_ConfigurableTurret Source => source;
        internal GuidedMissileSettings Settings => sourceDef.GetModExtension<GuidedMissileSettings>();
        internal CombinedDefenseAssets Assets => CombinedDefenseAssets.Get(Settings);
        internal TurretTargetingDef Targeting => sourceDef.GetModExtension<TurretAimSettings>().targeting;
        internal bool Clear => flight.Clear || source == null || !source.Spawned;
        public override int UpdateRateTicks => 1;
        public override Vector3 ExactPosition => flight.Ground(flight.Tip);
        public override Quaternion ExactRotation => flight.Rotation;
        public override Vector3 DrawPos => ExactPosition + Vector3.forward * Mathf.Max(0, flight.Tip.y - flight.Anchor.y)
            * (TurretCaptureProfile.Direction * Vector3.up).y;
        SiloSettings IMissileExhaustSource.ExhaustSettings => Settings.exhaust;
        Vector3 IMissileExhaustSource.ExhaustAnchor => flight.Anchor;
        Building_MissileSilo IMissileExhaustSource.ExhaustSilo => null;
        GameObject IMissileExhaustSource.FlamePrefab => Assets.Flame;
        GameObject IMissileExhaustSource.SmokePrefab => Assets.Smoke;
        int IMissileExhaustSource.TrailSeed => thingIDNumber;
        float IMissileExhaustSource.FlightSeconds => ageTicks / 60f;
        float IMissileExhaustSource.IgnitionSeconds => flight.IgnitionSeconds;
        float IMissileExhaustSource.EngineSeconds => Mathf.Max(0, ageTicks / 60f - flight.IgnitionSeconds);
        bool IMissileExhaustSource.EngineIgnited => flight.Ignited;

        //记录来源弹位并初始化冷发射姿态。
        internal void Initialize(Building_ConfigurableTurret owner, int slot, Vector3 tip, Quaternion rotation, Vector3 scale)
        {
            source = owner;
            sourceDef = owner.def;
            launchSlot = slot;
            flight.Initialize(tip, rotation, scale, owner.GetComp<OffscreenTurretComp>().Submission.Frame.GroundAnchor,
                Settings, Assets);
        }

        //初始化原版发射信息和导弹寿命。
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            ticksToImpact = lifetime = Settings.missileLifetimeTicks;
            ReadGuidanceTarget();
        }

        //向地图登记弹体和烟焰显示。
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.GetComponent<GuidedMissileMapVisuals>().Add(this);
        }

        //逐刻更新飞行，并检查寿命、近炸和地面碰撞。
        protected override void TickInterval(int delta)
        {
            for (int i = 0; i < delta && !Destroyed; i++)
            {
                if (!flight.Clear && source != null && source.Spawned)
                {
                    var muzzle = ((IGuidedMissileLauncher)source).GetMissileFirePoint(launchSlot);
                    flight.AlignEjection(muzzle.position, muzzle.rotation);
                }
                TurretTargetState? target = ReadGuidanceTarget();
                flight.Tick(Settings, target, (ageTicks + 1) / 60f);
                ageTicks++;
                var cell = ExactPosition.ToIntVec3();
                if (!cell.InBounds(Map) || ageTicks >= Settings.missileLifetimeTicks) { Destroy(); return; }
                Position = cell;
                if (target.HasValue && GuidedMissileImpact.Reached(flight, target.Value, Settings.fuseRadius))
                { Detonate(target.Value.Kind != TurretTargetKind.Ground); return; }
                if (GuidedMissileImpact.GroundCollision(this))
                { Detonate(flight.Tip.y - flight.Anchor.y > 2); return; }
            }
        }

        //跟踪地面目标的位置，目标失效后继续攻击其最后位置。
        private TurretTargetState? ReadGuidanceTarget()
        {
            if (TurretTargetResolver.TryRead(intendedTarget, Targeting.groundAimHeight, out var state))
            {
                hasGroundTarget = state.Kind == TurretTargetKind.Ground;
                if (hasGroundTarget) lastGroundTarget = state.GroundPosition;
                return state;
            }
            return hasGroundTarget
                ? new TurretTargetState(TurretTargetKind.Ground, lastGroundTarget, Targeting.groundAimHeight, Vector3.zero)
                : (TurretTargetState?)null;
        }

        //空爆结算空中伤害，地面命中交给原版爆炸流程。
        private void Detonate(bool airborne)
        {
            if (airborne) { GuidedMissileImpact.AirBurst(this); Destroy(); }
            else base.Impact(null);
        }

        //向防空接口提供导弹高度和三维速度。
        public bool TryGetTurretTarget(out TurretTargetState state)
        {
            Vector3 ground = flight.Ground(flight.Tip);
            Vector3 velocity = (ground - flight.Ground(flight.PreviousTip)) * 60;
            velocity.y = (flight.Tip.y - flight.PreviousTip.y) * 60;
            state = new TurretTargetState(TurretTargetKind.Spatial, ground, Mathf.Max(0, flight.Tip.y - flight.Anchor.y), velocity);
            return Clear;
        }

        //按时间插值尾喷口姿态。
        void IMissileExhaustSource.ExhaustPose(float seconds, out Quaternion rotation, out Vector3 tail)
        {
            flight.Pose(Mathf.Clamp01(seconds * 60 - (ageTicks - 1)), Assets, out _, out rotation, out tail);
        }

        //跳过原版贴图绘制，弹体由共享离屏相机显示。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false) { }

        //移除弹体显示，保留余烟直至消散。
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map.GetComponent<GuidedMissileMapVisuals>().Remove(this);
            base.DeSpawn(mode);
        }

        //读写来源弹位、弹龄、飞行状态和最后的地面目标位置。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref sourceDef, "sourceDef");
            Scribe_References.Look(ref source, "source");
            Scribe_Deep.Look(ref flight, "guidedFlight");
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref launchSlot, "launchSlot");
            Scribe_Values.Look(ref lastGroundTarget, "lastGroundTarget");
            Scribe_Values.Look(ref hasGroundTarget, "hasGroundTarget");
        }
    }
}
