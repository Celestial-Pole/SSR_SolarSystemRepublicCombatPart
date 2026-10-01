using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //维护逐弹位的自动补充和左右交替发射，不与近防炮的弹药或连发计时耦合。
    internal sealed class CombinedMissileLauncher : IExposable
    {
        private readonly Building_CombinedAirDefense owner;
        private List<int> cooldowns;
        private int nextSlot, interval;
        internal int ReadyCount => cooldowns.Count(value => value == 0);

        //按实际弹位数量初始化独立库存，读档时由深度存档恢复冷却。
        public CombinedMissileLauncher(Building_CombinedAirDefense owner)
        {
            this.owner = owner;
            cooldowns = Enumerable.Repeat(0, owner.CombinedSettings.missileSlots.Count).ToList();
        }

        //供电时推进各弹位冷却，瞄准完成后从下一个可用弹位发射。
        internal void Tick(bool powered, bool firing)
        {
            if (powered)
            {
                if (interval > 0) interval--;
                for (int i = 0; i < cooldowns.Count; i++) if (cooldowns[i] > 0) cooldowns[i]--;
                if (firing && interval == 0 && owner.TryReadTarget(owner.CurrentTarget, out var target)
                    && owner.Aim.Aligned(target)) TryLaunch(target);
            }
            owner.Rig.ShowPayloads(cooldowns);
        }

        //同步当前瞄准姿态，用实际弹位炮口建立冷发射弹丸，生成后消耗该弹位。
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

        //保存八个弹位和逐枚发射间隔，读档不重复补充或发射。
        public void ExposeData()
        {
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Value);
            Scribe_Values.Look(ref nextSlot, "nextSlot");
            Scribe_Values.Look(ref interval, "interval");
        }
    }
}
