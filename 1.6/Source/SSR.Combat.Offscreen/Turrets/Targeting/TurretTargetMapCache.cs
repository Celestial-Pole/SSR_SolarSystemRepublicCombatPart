using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //共享每张地图的空中候选和运动采样，避免每座近防炮反复遍历整张地图。
    public sealed class TurretTargetMapCache : MapComponent
    {
        private readonly List<Thing> airTargets = new List<Thing>();
        private readonly Dictionary<Thing, Sample> samples = new Dictionary<Thing, Sample>();
        private int scanTick = -100000;

        //保存最近一次目标采样，用游戏刻差分求出绘制型飞行目标的速度。
        private struct Sample { internal int Tick; internal TurretTargetState State; }

        //将缓存绑定到地图生命周期，缓存不参与存档。
        public TurretTargetMapCache(Map map) : base(map) { }

        //按请求的扫描间隔更新候选，返回共享只读列表。
        internal IReadOnlyList<Thing> AirTargets(int interval)
        {
            int tick = Find.TickManager.TicksGame;
            if (tick - scanTick >= interval)
            {
                scanTick = tick;
                airTargets.Clear();
                foreach (var thing in map.spawnedThings)
                    if (TurretTargetResolver.IsAirTarget(thing)) airTargets.Add(thing);
                foreach (var thing in samples.Keys.Where(t => !t.Spawned || t.Map != map).ToArray()) samples.Remove(thing);
            }
            return airTargets;
        }

        //读取统一状态，同一游戏刻复用速度，自定义三维接口直接提供自己的速度。
        internal bool TryRead(LocalTargetInfo target, TurretTargetingDef settings, out TurretTargetState state)
        {
            if (!TurretTargetResolver.TryRead(target, settings.groundAimHeight, out state)) return false;
            if (target.Thing == null || state.Kind == TurretTargetKind.Ground || TurretTargetResolver.SpatialProvider(target.Thing) != null)
                return true;
            int tick = Find.TickManager.TicksGame;
            if (samples.TryGetValue(target.Thing, out var previous))
            {
                Vector3 velocity = tick == previous.Tick ? previous.State.Velocity
                    : (state.Position - previous.State.Position) * (60f / (tick - previous.Tick));
                state = new TurretTargetState(state.Kind, state.GroundPosition, state.Height, velocity);
            }
            samples[target.Thing] = new Sample { Tick = tick, State = state };
            return true;
        }
    }
}
