using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理固定发射筒的装填和交替发射。
    internal sealed class CombinedMissileLauncher : IExposable
    {
        private readonly Building_CombinedAirDefense owner;
        private List<int> cooldowns;
        private int nextSlot, interval;
        internal int ReadyCount => cooldowns.Count(value => value == 0);

        //按配置弹位数初始化库存。
        public CombinedMissileLauncher(Building_CombinedAirDefense owner)
        {
            this.owner = owner;
            cooldowns = Enumerable.Repeat(0, owner.CombinedSettings.missileSlots.Count).ToList();
        }

        //推进装填，炮塔水平方位对准目标后沿固定筒口发射。
        internal void Tick(bool powered, bool firing)
        {
            owner.Aim.Prepare();
            var rig = owner.Rig;
            if (powered)
            {
                var settings = owner.CombinedSettings;
                if (interval > 0) interval--;
                for (int i = 0; i < cooldowns.Count; i++) if (cooldowns[i] > 0) cooldowns[i]--;
                if (firing && interval == 0 && ReadyCount > 0
                    && owner.TryReadTarget(owner.CurrentTarget, out var target)
                    && (target.GroundPosition - owner.MapDrawPosition).MagnitudeHorizontalSquared()
                        <= settings.missileRange * settings.missileRange
                    && rig.MissilesAligned(owner.Aim.TargetPoint(target), owner.Settings.yawAimTolerance)) TryLaunch();
            }
            rig.ShowPayloads(cooldowns);
        }

        //同步模型重建后的导弹库存显示。
        internal void Prepare(CombinedDefenseRig rig)
        {
            rig.ShowPayloads(cooldowns);
        }

        //从指定弹位创建导弹并扣除库存。
        private void TryLaunch()
        {
            var settings = owner.CombinedSettings;
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

        //读写库存和发射间隔。
        public void ExposeData()
        {
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Value);
            Scribe_Values.Look(ref nextSlot, "nextSlot");
            Scribe_Values.Look(ref interval, "interval");
        }
    }
}
