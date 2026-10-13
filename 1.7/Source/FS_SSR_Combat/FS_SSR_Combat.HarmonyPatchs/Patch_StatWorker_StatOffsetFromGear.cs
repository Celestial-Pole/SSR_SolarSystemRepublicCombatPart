using System;
using FS_SSR_Combat.Attachments;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(StatWorker), "StatOffsetFromGear")]
public static class Patch_StatWorker_StatOffsetFromGear
{
	public static void Postfix(Thing gear, StatDef stat, ref float __result)
	{
		try
		{
			if (gear is ISlotContainer slotContainer)
			{
				__result += slotContainer.StatCache.GetStatOffset(stat);
			}
		}
		catch (Exception arg)
		{
			Log.Warning($"[CAF] Exception in unified StatOffsetFromGear patch: {arg}");
		}
	}
}
