using System.Collections.Generic;
using FS_SSR_Combat.IntellectTargeting;
using Verse;

namespace FS_SSR_Combat;

public class MapComp_SSRCombatManger : MapComponent
{
	public List<TC_InvisibleKillerMap> invisibleKiller = new List<TC_InvisibleKillerMap>();

	private PotentialTargetCache targetCache;

	public MapComp_SSRCombatManger(Map map)
		: base(map)
	{
		targetCache = new PotentialTargetCache(map);
	}

	public bool HasMapInvisibleKiller()
	{
		if (!invisibleKiller.Any())
		{
			return false;
		}
		foreach (TC_InvisibleKillerMap item in invisibleKiller)
		{
			if (item.PowerOn)
			{
				return true;
			}
		}
		return false;
	}

	public void IntellectTargetUpdatePawn(Pawn p)
	{
		targetCache.UpdatePawn(p);
	}
}
