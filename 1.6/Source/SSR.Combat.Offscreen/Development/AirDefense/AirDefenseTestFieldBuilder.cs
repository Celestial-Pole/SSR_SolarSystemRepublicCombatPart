using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //在专用测试地图布置实际炮塔、电网和靶标，不暴露独立调试菜单入口。
    internal static class AirDefenseTestFieldBuilder
    {
        private static readonly string[] GroundTurrets =
        {
            "SSR_Turret_SentryGun", "SSR_Turret_PortableSentryGun",
            "SSR_Turret_Electromagnetic_145mmLtGun", "SSR_Turret_Electromagnetic_300mmMdGun",
            "SSR_Turret_Electromagnetic_500mmHvGun"
        };

        //在专用平地地图布置近防炮、弹炮合一系统、对地炮塔和空中靶标。
        internal static void Build(Map map, IntVec3 center)
        {
            var field = map.GetComponent<AirDefenseTestField>();
            var ciws = DefDatabase<ThingDef>.GetNamed("SSR_Turret_Electromagnetic_CIWS");
            for (int index = -1; index <= 1; index++)
                Spawn(ciws, center + new IntVec3(index * 10, 0, -8), map, field, Faction.OfPlayer);
            Spawn(DefDatabase<ThingDef>.GetNamed("SSR_Turret_CombinedAirDefense"),
                center + new IntVec3(20, 0, -8), map, field, Faction.OfPlayer);
            for (int index = 0; index < GroundTurrets.Length; index++)
                Spawn(DefDatabase<ThingDef>.GetNamed(GroundTurrets[index]),
                    center + new IntVec3((index - 2) * 8, 0, -19), map, field, Faction.OfPlayer);
            Spawn(DefDatabase<ThingDef>.GetNamed("SSR_AirDefenseTestPower"),
                center + new IntVec3(0, 0, -13), map, field, Faction.OfPlayer);
            var conduit = DefDatabase<ThingDef>.GetNamed("PowerConduit");
            for (int x = -20; x <= 20; x++)
                if (x != 0) Spawn(conduit, center + new IntVec3(x, 0, -13), map, field, Faction.OfPlayer);
            var target = Spawn(DefDatabase<ThingDef>.GetNamed("SSR_AirDefenseTestTarget"),
                center + new IntVec3(0, 0, 12), map, field, Faction.OfMechanoids);
            Find.Selector.ClearSelection();
            Find.Selector.Select(target);
            Messages.Message("防空测试场地已创建。选择靶标切换工况；近防炮自动索敌，对地炮塔保留原目标策略。",
                MessageTypeDefOf.TaskCompletion, false);
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
