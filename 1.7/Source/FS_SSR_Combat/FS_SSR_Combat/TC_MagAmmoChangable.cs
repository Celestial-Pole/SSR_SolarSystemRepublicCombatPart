using System.Collections.Generic;
using FS_SSR;
using FS_SSR.Generic;
using RimWorld;
using Verse;

namespace FS_SSR_Combat;

//管理可切换弹药的装填组件。
public class TC_MagAmmoChangable : CompApparelReloadable
{
	public int chosenAmmoIndex;

	private TCP_MagAmmoChangable Prop => (TCP_MagAmmoChangable)props;

	public ThingDef ChosenAmmo => Prop.ammoCandidates[chosenAmmoIndex];

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref chosenAmmoIndex, "ammoIndex", 0);
	}

	//显示当前弹药与对应图标。
	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		Gizmo_RightClickOptionsSelect val = new Gizmo_RightClickOptionsSelect
		{
			options = SelectOptions()
		};
		((Command)val).defaultLabel = "SSRC_CurAmmo".Translate() + ChosenAmmo.label;
		((Command)val).defaultDesc = "SSRC_CurAmmoDesc".Translate(ChosenAmmo.label);
		((Command)val).icon = ChosenAmmo.uiIcon;
		((Command_Action)val).action = delegate
		{
		};
		yield return (Gizmo)val;
	}

	private IEnumerable<FloatMenuOption> SelectOptions()
	{
		int i;
		for (i = 0; i < Prop.ammoCandidates.Count; i++)
		{
			ThingDef thingDef = Prop.ammoCandidates[i];
			yield return new FloatMenuOption("SSRC_SetAmmo".Translate() + thingDef.label, delegate
			{
				chosenAmmoIndex = i;
			});
		}
	}

	public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
	{
		return CompGetGizmosExtra();
	}
}
