using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //在有效世界地块上创建独立平地测试地图并切换镜头。
    internal static class AirDefenseTestMapActions
    {
        //从当前地图建立独立测试地图，完成加载后布置场地并切换视图。
        [DebugAction("SSR", "防空测试：创建新的平地地图", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Create()
        {
            var generator = DefDatabase<MapGeneratorDef>.GetNamed("SSR_AirDefenseTestMap");
            LongEventHandler.QueueLongEvent(() =>
            {
                //普通地图必须绑定真实地块，温度和生长率初始化依赖世界地块索引。
                var tile = Find.WorldGrid.Surface.Tiles.FirstOrDefault(candidate =>
                    candidate.hilliness == Hilliness.Flat && candidate.Mutators.Count == 0
                    && candidate.PrimaryBiome.extraGenSteps.Count == 0
                    && TileFinder.IsValidTileForNewSettlement(candidate.tile));
                if (tile == null) throw new InvalidOperationException("没有可用于防空测试的空闲平地世界地块。");
                var parent = (MapParent)WorldObjectMaker.MakeWorldObject(
                    DefDatabase<WorldObjectDef>.GetNamed("SSR_AirDefenseTestWorldObject"));
                parent.Tile = tile.tile;
                parent.SetFaction(Faction.OfPlayer);
                Find.WorldObjects.Add(parent);
                try
                {
                    var map = MapGenerator.GenerateMap(new IntVec3(160, 1, 160), parent, generator);
                    LongEventHandler.ExecuteWhenFinished(() => Enter(map));
                }
                catch
                {
                    //生成失败时移除未完成的地图，防止地图组件继续逐刻访问未初始化资源。
                    parent.Destroy();
                    throw;
                }
            }, "GeneratingMap", false, null);
        }

        //显示已生成的地图，清除迷雾并布置生产用炮塔和测试靶标。
        private static void Enter(Map map)
        {
            Current.Game.CurrentMap = map;
            Find.World.renderer.wantedMode = WorldRenderMode.None;
            map.fogGrid.ClearAllFog();
            AirDefenseTestFieldBuilder.Build(map, map.Center);
            Find.CameraDriver.SetRootPosAndSize(map.Center.ToVector3Shifted() - Vector3.forward * 12, 40);
        }
    }
}
