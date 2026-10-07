using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理导弹装填、交替发射和独立俯仰。
    internal sealed class CombinedMissileLauncher : IExposable
    {
        private readonly Building_CombinedAirDefense owner;
        private List<int> cooldowns;
        private int nextSlot, interval, idleTicks;
        private float pitch, pitchVelocity;
        internal int ReadyCount => cooldowns.Count(value => value == 0);

        //按配置弹位数初始化库存。
        public CombinedMissileLauncher(Building_CombinedAirDefense owner)
        {
            this.owner = owner;
            cooldowns = Enumerable.Repeat(0, owner.CombinedSettings.missileSlots.Count).ToList();
        }

        //推进装填和瞄准，依次使用就绪弹位。
        internal void Tick(bool powered, bool firing)
        {
            owner.Aim.Prepare();
            var rig = owner.Rig;
            Prepare(rig);
            bool hasTarget = owner.TryReadTarget(owner.CurrentTarget, out var target);
            Vector3 point = hasTarget ? owner.Aim.TargetPoint(target) : Vector3.zero;
            if (powered)
            {
                float desired = pitch;
                if (hasTarget)
                {
                    idleTicks = 0;
                    desired = rig.SolveMissilePitch(point, pitch, owner.Settings.solverIterations);
                }
                else if (owner.Settings.returnToIdle && ++idleTicks >= owner.Settings.idleDelayTicks) desired = 0;
                var settings = owner.CombinedSettings;
                pitch = TurretAngleLimits.Advance(pitch, desired, ref pitchVelocity, settings.missilePitchSpeed,
                    settings.missilePitchAcceleration, settings.missilePitchRange);
                rig.ApplyMissilePitch(pitch);
                if (interval > 0) interval--;
                for (int i = 0; i < cooldowns.Count; i++) if (cooldowns[i] > 0) cooldowns[i]--;
                if (firing && interval == 0 && hasTarget && rig.MissilesAligned(point, owner.Settings.aimConeTolerance)) TryLaunch(target);
            }
            else pitchVelocity = 0;
            rig.ShowPayloads(cooldowns);
        }

        //同步导弹架俯仰和库存显示。
        internal void Prepare(CombinedDefenseRig rig)
        {
            rig.ApplyMissilePitch(pitch);
            rig.ShowPayloads(cooldowns);
        }

        //从指定弹位创建导弹并扣除库存。
        private void TryLaunch(TurretTargetState target)
        {
            var settings = owner.CombinedSettings;
            if ((target.GroundPosition - owner.MapDrawPosition).MagnitudeHorizontalSquared() > settings.missileRange * settings.missileRange) return;
            owner.Aim.Prepare();
            for (int offset = 0; offset < cooldowns.Count; offset++)
            {
                int slot = (nextSlot + offset) % cooldowns.Count;
                if (cooldowns[slot] != 0) continue;
                var payload = owner.Rig.Payloads[slot];
                var fire = owner.Rig.FirePoints[slot];
                var missile = (Projectile_GuidedDefenseMissile)ThingMaker.MakeThing(settings.missileProjectile);
                missile.Initialize(owner, slot, fire.position, fire.rotation, payload.lossyScale);
                GenSpawn.Spawn(missile, owner.Position, owner.Map);
                missile.Launch(owner, owner.DrawPos, owner.CurrentTarget, owner.CurrentTarget,
                    ProjectileHitFlags.IntendedTarget, true, owner.gun);
                cooldowns[slot] = settings.reloadTicks;
                interval = settings.launchIntervalTicks;
                nextSlot = (slot + 1) % cooldowns.Count;
                return;
            }
        }

        //读写库存、发射间隔和俯仰状态。
        public void ExposeData()
        {
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Value);
            Scribe_Values.Look(ref nextSlot, "nextSlot");
            Scribe_Values.Look(ref interval, "interval");
            Scribe_Values.Look(ref pitch, "pitch");
            Scribe_Values.Look(ref pitchVelocity, "pitchVelocity");
            Scribe_Values.Look(ref idleTicks, "idleTicks");
        }
    }
}
