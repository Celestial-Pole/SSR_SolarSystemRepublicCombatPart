using System.Collections.Generic;
using Verse;

namespace HJ_SSR.Weapons;

public class CompEquippedGizmo : ThingComp
{
	public virtual IEnumerable<Gizmo> CompGetGizmosEquipped()
	{
		yield return null;
	}
}
