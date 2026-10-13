using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FS_TransWeapon;

public class CachedWeaponStat
{
	public Dictionary<StatDef, float> statBases = new Dictionary<StatDef, float>();

	public List<VerbProperties> verbs = new List<VerbProperties>();

	public CachedWeaponStat(ThingDef w)
	{
		foreach (StatModifier statBasis in w.statBases)
		{
			statBases.Add(statBasis.stat, statBasis.value);
		}
		verbs = w.Verbs;
	}
}
