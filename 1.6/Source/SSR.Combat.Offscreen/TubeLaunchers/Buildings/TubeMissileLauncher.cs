using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //管理筒式发射器的库存、舱盖和导弹生成。
    internal sealed class TubeMissileLauncher : IExposable
    {
        private readonly Building_TubeMissileTurret owner;
        private TubeMissileRig rig;
        private List<int> cooldowns;
        private int nextSlot, holdOpenTicks;
        private float opening;
        private bool reloadingAfterEmpty;
        internal int ReadyCount => cooldowns.Count(value => value == 0);
        internal bool CanStartBurst => !reloadingAfterEmpty && (owner.LauncherSettings.allowPartialSalvo
            ? ReadyCount > 0 : ReadyCount == cooldowns.Count);
        internal bool DoorsOpen => owner.LauncherSettings.doors.Count == 0 || opening >= 1;

        //按配置弹位数初始化库存。
        public TubeMissileLauncher(Building_TubeMissileTurret owner)
        {
            this.owner = owner;
            cooldowns = Enumerable.Repeat(0, owner.LauncherSettings.missileSlots.Count).ToList();
        }

        //绑定模型并同步库存和舱盖姿态。
        internal TubeMissileRig Prepare()
        {
            owner.Aim.Prepare();
            var model = owner.GetComp<OffscreenTurretComp>().CurrentUnityObject.ownGameObject;
            if (rig == null || rig.Model != model) rig = new TubeMissileRig(model, owner.LauncherSettings);
            rig.Apply(cooldowns, opening);
            return rig;
        }

        //供电时推进装填，对准目标后开盖，齐射期间保持打开。
        internal void Tick()
        {
            if (owner.MechanismsActive)
            {
                if (holdOpenTicks > 0) holdOpenTicks--;
                for (int i = 0; i < cooldowns.Count; i++) if (cooldowns[i] > 0) cooldowns[i]--;
                if (reloadingAfterEmpty && ReadyCount == cooldowns.Count) reloadingAfterEmpty = false;
                bool open = holdOpenTicks > 0 || owner.CanContinue && ReadyCount > 0
                    && owner.TryReadTarget(owner.CurrentTarget, out var target)
                    && (owner.AttackVerb.state == VerbState.Bursting || owner.Aim.Aligned(target));
                opening = Mathf.MoveTowards(opening, open ? 1 : 0, 1f / owner.LauncherSettings.openingTicks);
            }
            Prepare();
        }

        //按弹位姿态生成导弹并扣除库存。
        internal bool TryLaunch(LocalTargetInfo target)
        {
            if (!owner.CanFireAt(target)) return false;
            var current = Prepare();
            var settings = owner.LauncherSettings;
            for (int offset = 0; offset < cooldowns.Count; offset++)
            {
                int slot = (nextSlot + offset) % cooldowns.Count;
                if (cooldowns[slot] != 0) continue;
                var fire = current.FirePoints[slot];
                var scale = current.Payloads[slot].lossyScale;
                var missile = (Projectile_GuidedDefenseMissile)ThingMaker.MakeThing(settings.missileProjectile);
                missile.Initialize(owner, slot, fire.position, fire.rotation, scale);
                GenSpawn.Spawn(missile, owner.Position, owner.Map);
                missile.Launch(owner, owner.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, true, owner.gun);
                cooldowns[slot] = settings.reloadTicks;
                reloadingAfterEmpty = ReadyCount == 0;
                nextSlot = (slot + 1) % cooldowns.Count;
                var assets = CombinedDefenseAssets.Get(settings);
                float stroke = (assets.Tip.z - assets.Trail.z) * scale.z + settings.ejectionClearance;
                holdOpenTicks = Mathf.CeilToInt(120 * stroke / settings.ejectionSpeed) + 1;
                current.Apply(cooldowns, opening);
                return true;
            }
            return false;
        }

        //读写库存、舱盖和筒内保持时间。
        public void ExposeData()
        {
            Scribe_Collections.Look(ref cooldowns, "cooldowns", LookMode.Value);
            Scribe_Values.Look(ref nextSlot, "nextSlot");
            Scribe_Values.Look(ref holdOpenTicks, "holdOpenTicks");
            Scribe_Values.Look(ref opening, "opening");
            Scribe_Values.Look(ref reloadingAfterEmpty, "reloadingAfterEmpty");
        }
    }
}
