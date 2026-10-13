using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FS_SSR_Combat;

public class TCP_MagAmmoChangable : CompProperties_ApparelReloadable
{
	public List<ThingDef> ammoCandidates = new List<ThingDef>();

	public TCP_MagAmmoChangable()
	{
		compClass = typeof(TC_MagAmmoChangable);
	}
}
