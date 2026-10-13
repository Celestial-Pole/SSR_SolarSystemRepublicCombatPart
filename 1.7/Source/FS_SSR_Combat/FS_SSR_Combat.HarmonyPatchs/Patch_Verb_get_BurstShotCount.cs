using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.CustomWeapon;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(Verb), "get_BurstShotCount")]
public static class Patch_Verb_get_BurstShotCount
{
	public static void Postfix(Verb __instance, ref int __result)
	{
		if (!(__instance.EquipmentSource is SSR_CustomWeapon sSR_CustomWeapon))
		{
			return;
		}
		float num = 1f;
		foreach (IAttachable allInstalledAttachment in sSR_CustomWeapon.AllInstalledAttachments)
		{
			if (!(allInstalledAttachment is AttachmentBase { IsBroken: not false }))
			{
				num *= allInstalledAttachment.def.weaponProperties?.burstShotCountMultiplier ?? 1f;
			}
		}
		if (!Mathf.Approximately(num, 1f))
		{
			__result = Mathf.CeilToInt((float)__result * num);
		}
	}
}
