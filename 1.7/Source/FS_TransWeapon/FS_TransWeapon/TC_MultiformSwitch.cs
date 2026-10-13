using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FS_TransWeapon;

public class TC_MultiformSwitch : ThingComp
{
	public MultiformWeapon Parent => (MultiformWeapon)parent;

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (Parent.holdingOwner == null || !(Parent.holdingOwner.Owner is Thing { Faction: var faction }) || (faction != null && faction == Faction.OfPlayer))
		{
			yield return new Command_Action
			{
				defaultLabel = "切换武器",
				defaultDesc = "desc",
				icon = Parent.ActualDef.uiIcon,
				action = Action
			};
		}
	}

	public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
	{
		return CompGetWornGizmosExtra();
	}

	public virtual void Action()
	{
		int i = 0;
		if (Parent.currentWeapon == 0)
		{
			i = 1;
		}
		Parent.ChangeWeaponTo(i);
	}
}
