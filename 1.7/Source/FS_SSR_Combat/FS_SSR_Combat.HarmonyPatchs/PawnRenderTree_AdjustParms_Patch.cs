using System.Linq;
using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.CustomArmor;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(PawnRenderTree), "AdjustParms")]
public static class PawnRenderTree_AdjustParms_Patch
{
	public static void Postfix(PawnRenderTree __instance, ref PawnDrawParms parms)
	{
		Pawn pawn = __instance.pawn;
		if (pawn?.apparel?.WornApparel == null)
		{
			return;
		}
		foreach (SSR_CustomArmor item in pawn.apparel.WornApparel.OfType<SSR_CustomArmor>())
		{
			if (ShouldHideHairRecursive(item))
			{
				parms.skipFlags |= RenderSkipFlagDefOf.Hair;
				break;
			}
		}
	}

	private static bool ShouldHideHairRecursive(ISlotContainer container)
	{
		foreach (IAttachable allInstalledAttachment in container.AllInstalledAttachments)
		{
			if (!(allInstalledAttachment is AttachmentBase { IsBroken: not false }))
			{
				AttachmentDef.ApparelProperties apparelProperties = allInstalledAttachment.def.apparelProperties;
				if (apparelProperties != null && apparelProperties.hidesHair)
				{
					return true;
				}
				if (allInstalledAttachment is ISlotContainer container2 && ShouldHideHairRecursive(container2))
				{
					return true;
				}
			}
		}
		return false;
	}
}
