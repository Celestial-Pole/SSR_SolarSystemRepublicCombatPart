using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //协调筒式发射器的瞄准、库存和连发。
    public sealed class Building_TubeMissileTurret : Building_ConfigurableTurret, IGuidedMissileLauncher
    {
        private static readonly AccessTools.FieldRef<Building_TurretGun, bool> ReadHoldFire
            = AccessTools.FieldRefAccess<Building_TurretGun, bool>("holdFire");
        private TubeMissileLauncher launcher;
        internal TubeLauncherSettings LauncherSettings => def.GetModExtension<TubeLauncherSettings>();
        internal TubeMissileLauncher Launcher => launcher ?? (launcher = new TubeMissileLauncher(this));
        internal bool MechanismsActive => Spawned && Active && !IsStunned;
        internal bool CanContinue => MechanismsActive && !ReadHoldFire(this);

        //先更新发射器，再推进原版射击计时。
        protected override void Tick()
        {
            Launcher.Tick();
            base.Tick();
        }

        //检查连发条件；瞄准对齐仅在 BeginBurst 首发时检查。
        public override bool CanFireAt(LocalTargetInfo target)
        {
            return CanContinue && Launcher.ReadyCount > 0 && Launcher.DoorsOpen && TryReadTarget(target, out _);
        }

        //等待舱盖打开后开始连发，无效或无法瞄准的目标交还原有处理。
        protected override void BeginBurst()
        {
            if (!CanContinue || Launcher.ReadyCount == 0) { burstWarmupTicksLeft = 0; return; }
            if (!Launcher.DoorsOpen && TryReadTarget(CurrentTarget, out var state) && Aim.CanReach(state))
            { burstWarmupTicksLeft = 1; return; }
            base.BeginBurst();
        }

        //返回当前导弹发射挂点。
        public Transform GetMissileFirePoint(int slot) => Launcher.Prepare().FirePoints[slot];

        //绘制前同步舱盖和库存弹体。
        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Launcher.Prepare();
            base.DrawAt(drawLoc, flip);
        }

        //读写发射器库存和舱盖状态。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref launcher, "tubeLauncher", this);
        }

        //显示待发数量和总弹位数。
        public override string GetInspectString()
        {
            return base.GetInspectString() + "\n待发弹体：" + Launcher.ReadyCount + " / " + LauncherSettings.missileSlots.Count;
        }
    }
}
