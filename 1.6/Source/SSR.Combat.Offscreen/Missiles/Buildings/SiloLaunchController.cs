using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //以独立舱盖时钟管理开一舱发一枚、离井等待、关盖和无限补弹。
    internal sealed class SiloLaunchController : IExposable
    {
        //标识可持久化的发射阶段，等待离井时保持当前开盖姿态。
        private enum Phase { Ready, Opening, Interval, Clearing, Closing, Cooling }
        private readonly Building_MissileSilo owner;
        private Phase phase;
        private int timer, nextSlot, loadedMask = 511;
        private List<int> lidTicks = Enumerable.Repeat(0, 9).ToList();
        private List<IntVec3> targets = new List<IntVec3>();
        private List<Projectile_VerticalMissile> launched = new List<Projectile_VerticalMissile>();
        internal bool Ready => phase == Phase.Ready;
        internal int LoadedCount => Enumerable.Range(0, 9).Count(IsLoaded);
        internal string Status => new[] { "待命", "逐舱开盖发射", "发射间隔", "等待导弹离井", "关盖", "冷却补弹" }[(int)phase];

        //绑定所属建筑，读档通过相同构造参数恢复控制器。
        public SiloLaunchController(Building_MissileSilo owner) { this.owner = owner; }

        //检查对应库存展示弹是否仍在舱内。
        internal bool IsLoaded(int slot) { return (loadedMask & (1 << slot)) != 0; }

        //返回指定舱盖的独立动画时间，未启动的舱盖始终停在闭合帧。
        internal float LidTime(int slot) { return Mathf.Min(lidTicks[slot] / 60f, owner.Assets.Layout.slots[slot].openingSeconds); }

        //将独立舱盖的动画时长转换为不会提前发射的整数游戏刻。
        private int OpeningTicks(int slot) { return Mathf.CeilToInt(owner.Assets.Layout.slots[slot].openingSeconds * 60); }

        //开盖前锁定唯一落点，九个舱位共享该点，目标移动和手动换目标只影响下一轮。
        internal bool Begin(LocalTargetInfo target)
        {
            if (!Ready || !owner.CanContinue || !target.IsValid || !target.Cell.InBounds(owner.Map)) return false;
            IntVec3 lockedCell = target.Cell;
            targets.Clear();
            for (int i = 0; i < 9; i++) targets.Add(lockedCell);
            launched.Clear();
            nextSlot = timer = 0;
            for (int i = 0; i < 9; i++) lidTicks[i] = 0;
            phase = Phase.Opening;
            return true;
        }

        //推进阶段并让停火只取消尚未发出的导弹，已发射弹丸继续飞行。
        internal void Tick()
        {
            if ((phase == Phase.Opening || phase == Phase.Interval) && !owner.CanContinue) phase = Phase.Clearing;
            switch (phase)
            {
                case Phase.Opening:
                    if (++lidTicks[nextSlot] < OpeningTicks(nextSlot)) break;
                    //只在当前舱盖完全打开的同一刻弹出该舱导弹，不等待其他八个舱盖。
                    LaunchNext();
                    if (nextSlot == 9) { phase = Phase.Clearing; break; }
                    //把下一扇盖子的运动计入发射间隔，盖子打开后不附加等待。
                    timer = Mathf.Max(0, Mathf.RoundToInt(owner.Settings.launchInterval * 60) - OpeningTicks(nextSlot));
                    phase = timer > 0 ? Phase.Interval : Phase.Opening;
                    break;
                case Phase.Interval:
                    if (timer > 0) timer--;
                    if (timer == 0) phase = Phase.Opening;
                    break;
                case Phase.Clearing:
                    if (launched.All(m => m == null || m.Destroyed || m.ClearOfSilo)) phase = Phase.Closing;
                    break;
                case Phase.Closing:
                    for (int i = 0; i < 9; i++) if (lidTicks[i] > 0) lidTicks[i]--;
                    if (lidTicks.All(t => t == 0)) { phase = Phase.Cooling; timer = Mathf.RoundToInt(owner.Settings.cooldownSeconds * 60); }
                    break;
                case Phase.Cooling:
                    if (timer > 0) timer--;
                    if (timer == 0) { loadedMask = 511; launched.Clear(); targets.Clear(); phase = Phase.Ready; }
                    break;
            }
        }

        //从实际库存姿态创建弹丸，并在同一逻辑步移除对应展示弹。
        private void LaunchNext()
        {
            var projectile = (Projectile_VerticalMissile)ThingMaker.MakeThing(owner.AttackVerb.verbProps.defaultProjectile);
            projectile.Initialize(owner, nextSlot, targets[nextSlot]);
            GenSpawn.Spawn(projectile, owner.Position, owner.Map);
            projectile.Launch(owner, owner.DrawPos, targets[nextSlot], targets[nextSlot], ProjectileHitFlags.None, false, owner.gun);
            launched.Add(projectile);
            loadedMask &= ~(1 << nextSlot);
            nextSlot++;
        }

        //保存计时、库存、落点和在途引用，读档不会重新抽取目标或补发上一枚。
        public void ExposeData()
        {
            Scribe_Values.Look(ref phase, "phase");
            Scribe_Collections.Look(ref lidTicks, "lidTicks", LookMode.Value);
            Scribe_Values.Look(ref timer, "timer");
            Scribe_Values.Look(ref nextSlot, "nextSlot");
            Scribe_Values.Look(ref loadedMask, "loadedMask", 511);
            Scribe_Collections.Look(ref targets, "targets", LookMode.Value);
            Scribe_Collections.Look(ref launched, "launched", LookMode.Reference);
        }
    }
}
