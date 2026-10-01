using RimWorld;
using Verse;

namespace SSR.Combat.Offscreen
{
    //为独立防空地图生成无山体、无植被和无屋顶的平坦混凝土场地。
    public sealed class GenStep_AirDefenseTestTerrain : GenStep
    {
        //提供固定的步骤种子标识，地形本身不依赖随机数。
        public override int SeedPart => 176923401;

        //铺设整张地图的统一地面并指定中心出生参考点，省略自然地形和物体生成。
        public override void Generate(Map map, GenStepParams parms)
        {
            using (map.pathing.DisableIncrementalScope())
                foreach (var cell in map.AllCells)
                {
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
                    map.roofGrid.SetRoof(cell, null);
                }
            MapGenerator.PlayerStartSpot = map.Center;
        }
    }
}
