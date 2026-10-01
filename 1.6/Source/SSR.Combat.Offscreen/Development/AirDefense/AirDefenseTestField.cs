using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //登记开发场地炮塔并补充测试弹药，向靶标面板提供炮塔检查列表。
    public sealed class AirDefenseTestField : MapComponent
    {
        private List<Building_ConfigurableTurret> turrets = new List<Building_ConfigurableTurret>();
        //仅列出场地内仍在地图上的生产用炮塔，供检查与弹药维护读取。
        internal IEnumerable<Building_ConfigurableTurret> Turrets => turrets.Where(turret => turret.Spawned);

        //绑定当前地图，未创建场地时不干预正常游戏。
        public AirDefenseTestField(Map map) : base(map) { }

        //登记由地图创建入口生成的炮塔，其他场地物体由地图自身管理。
        internal void Register(Building_ConfigurableTurret turret) { turrets.Add(turret); }

        //定期补充场地炮塔弹药，电力仍通过场地发电机和原版电网供应。
        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var turret in Turrets)
            {
                var fuel = turret.GetComp<CompRefuelable>();
                if (fuel != null) fuel.Refuel(fuel.Props.fuelCapacity - fuel.Fuel);
            }
        }

        //保存炮塔引用，加载后继续补充弹药并提供俯仰状态检查。
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref turrets, "airDefenseTestTurrets", LookMode.Reference);
        }
    }
}
