using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //在专用测试地图布置全部炮塔、电网和靶标。
    internal static class AirDefenseTestFieldBuilder
    {
        private static readonly string[] Turrets =
        {
            "SSR_Turret_MissileBox", "SSR_Turret_Electromagnetic_CIWS",
            "SSR_Turret_CombinedAirDefense", "SSR_Turret_RocketArtillery",
            "SSR_Turret_SentryGun", "SSR_Turret_PortableSentryGun",
            "SSR_Turret_EMSentry", "SSR_Turret_PortableEMSentry", "SSR_Turret_EMAutocannon",
            "SSR_Turret_Electromagnetic_145mmLtGun", "SSR_Turret_Electromagnetic_300mmMdGun",
            "SSR_Turret_Electromagnetic_500mmHvGun", "SSR_Turret_MissileSilo"
        };

        //在平地地图布置全部型号和空中靶标。
        internal static void Build(Map map, IntVec3 center)
        {
            var field = map.GetComponent<AirDefenseTestField>();
            for (int index = 0; index < Turrets.Length; index++)
            {
                var definition = DefDatabase<ThingDef>.GetNamed(Turrets[index]);
                var offset = index == 12 ? new IntVec3(35, 0, -8)
                    : new IntVec3(-21 + index % 4 * 14, 0, -8 - index / 4 * 16);
                Spawn(definition, center + offset, map, field, Faction.OfPlayer);
            }
            BuildPower(map, center, field);
            var target = Spawn(DefDatabase<ThingDef>.GetNamed("SSR_AirDefenseTestTarget"),
                center + new IntVec3(0, 0, 12), map, field, Faction.OfMechanoids);
            Find.Selector.ClearSelection();
            Find.Selector.Select(target);
            Messages.Message("防空测试场地已创建，包含全部 13 种炮塔。选择靶标切换工况，或用炮塔的“角度预览”检查姿态。",
                MessageTypeDefOf.TaskCompletion, false);
        }

        //连接各排炮塔，电源与导线距炮塔不超过原版接线范围。
        private static void BuildPower(Map map, IntVec3 center, AirDefenseTestField field)
        {
            var powerPosition = center + new IntVec3(-29, 0, -12);
            Spawn(DefDatabase<ThingDef>.GetNamed("SSR_AirDefenseTestPower"), powerPosition, map, field, Faction.OfPlayer);
            var conduit = DefDatabase<ThingDef>.GetNamed("PowerConduit");
            var wires = new HashSet<IntVec3>();
            for (int row = 0; row < 3; row++)
                for (int x = -29; x <= (row == 0 ? 35 : 21); x++)
                    wires.Add(center + new IntVec3(x, 0, -12 - row * 16));
            for (int z = -43; z < -12; z++) wires.Add(center + new IntVec3(-29, 0, z));
            foreach (var cell in wires)
                if (cell != powerPosition) Spawn(conduit, cell, map, field, Faction.OfPlayer);
        }

        //按实际定义生成实体并登记炮塔检查列表，不复制或替换生产用瞄准配置。
        private static Thing Spawn(ThingDef definition, IntVec3 position, Map map, AirDefenseTestField field, Faction faction)
        {
            var thing = ThingMaker.MakeThing(definition);
            thing.SetFaction(faction);
            GenSpawn.Spawn(thing, position, map);
            if (thing is Building_ConfigurableTurret turret) field.Register(turret);
            return thing;
        }
    }
}
