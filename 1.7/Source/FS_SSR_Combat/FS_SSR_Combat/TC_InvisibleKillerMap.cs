using RimWorld;
using Verse;

namespace FS_SSR_Combat;

public class TC_InvisibleKillerMap : ThingComp
{
	private MapComp_SSRCombatManger mapComp;

	public bool PowerOn
	{
		get
		{
			CompPowerTrader compPowerTrader = parent.TryGetComp<CompPowerTrader>();
			if (compPowerTrader != null && !compPowerTrader.PowerOn)
			{
				return false;
			}
			return true;
		}
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		mapComp = parent.Map.GetComponent<MapComp_SSRCombatManger>();
		if (!mapComp.invisibleKiller.Contains(this))
		{
			mapComp.invisibleKiller.Add(this);
		}
	}

	public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
	{
		mapComp.invisibleKiller.Remove(this);
		base.PostDeSpawn(map);
	}
}
