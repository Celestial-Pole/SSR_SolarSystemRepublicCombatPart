using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //持久化独立制导弹丸，提供真实高度、模型姿态和与导弹井共用的喷口历史。
    public sealed class Projectile_GuidedDefenseMissile : Projectile_Explosive, ITurretSpatialTarget, IMissileExhaustSource
    {
        private ThingDef sourceDef;
        private Building_CombinedAirDefense source;
        private GuidedMissileFlight flight = new GuidedMissileFlight();
        private int ageTicks, launchSlot;
        internal GuidedMissileFlight Flight => flight;
        internal Building_CombinedAirDefense Source => source;
        internal CombinedAirDefenseSettings Settings => sourceDef.GetModExtension<CombinedAirDefenseSettings>();
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

        //在出生前记录实际弹位及冷发射姿态，使筒内弹体能够持续跟随发射器瞄准轴。
        internal void Initialize(Building_CombinedAirDefense owner, int slot, Vector3 tip, Quaternion rotation, Vector3 scale)
        {
            source = owner;
            sourceDef = owner.def;
            launchSlot = slot;
            flight.Initialize(tip, rotation, scale, owner.GetComp<OffscreenTurretComp>().Submission.Frame.GroundAnchor,
                Settings, Assets);
        }

        //保留原版发射者与伤害信息，实际飞行由三维制导积分负责。
        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            ticksToImpact = lifetime = Settings.missileLifetimeTicks;
        }

        //登记飞行模型与共用烟焰，镜头外同样推进制导和寿命。
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            map.GetComponent<GuidedMissileMapVisuals>().Add(this);
        }

        //筒内逐刻对齐发射器，离筒后滑行并延时点火；失去目标时继续最后航向。
        protected override void TickInterval(int delta)
        {
            for (int i = 0; i < delta && !Destroyed; i++)
            {
                if (!flight.Clear && source != null && source.Spawned)
                {
                    source.Aim.Prepare();
                    var muzzle = source.Rig.FirePoints[launchSlot];
                    flight.AlignEjection(muzzle.position, muzzle.rotation);
                }
                TurretTargetState? target = TurretTargetResolver.TryRead(intendedTarget,
                    Targeting.groundAimHeight,
                    out var state) ? state : (TurretTargetState?)null;
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

        //分别结算空中拦截与实际地面爆炸，禁止在高空向地面投影位置施加伤害。
        private void Detonate(bool airborne)
        {
            if (airborne) { GuidedMissileImpact.AirBurst(this); Destroy(); }
            else base.Impact(null);
        }

        //向防空接口提供真实高度和三维速度，使敌方炮塔能够拦截该导弹。
        public bool TryGetTurretTarget(out TurretTargetState state)
        {
            Vector3 ground = flight.Ground(flight.Tip);
            Vector3 velocity = (ground - flight.Ground(flight.PreviousTip)) * 60;
            velocity.y = (flight.Tip.y - flight.PreviousTip.y) * 60;
            state = new TurretTargetState(TurretTargetKind.Spatial, ground, Mathf.Max(0, flight.Tip.y - flight.Anchor.y), velocity);
            return Clear;
        }

        //采样真实喷口的刻内运动，尾焰与弯曲烟迹沿同一条飞行历史生成。
        void IMissileExhaustSource.ExhaustPose(float seconds, out Quaternion rotation, out Vector3 tail)
        {
            flight.Pose(Mathf.Clamp01(seconds * 60 - (ageTicks - 1)), Assets, out _, out rotation, out tail);
        }

        //三维弹体由共享离屏相机绘制，不叠加原版二维导弹贴图。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false) { }

        //停止喷口采样并释放弹体，已经生成的烟焰继续自然消散。
        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map.GetComponent<GuidedMissileMapVisuals>().Remove(this);
            base.DeSpawn(mode);
        }

        //保存制导位置、锁定对象和出筒阶段，读档不重复发射或结算伤害。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref sourceDef, "sourceDef");
            Scribe_References.Look(ref source, "source");
            Scribe_Deep.Look(ref flight, "guidedFlight");
            Scribe_Values.Look(ref ageTicks, "ageTicks");
            Scribe_Values.Look(ref launchSlot, "launchSlot");
        }
    }
}
