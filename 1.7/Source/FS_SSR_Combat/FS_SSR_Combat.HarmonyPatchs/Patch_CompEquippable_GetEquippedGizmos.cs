using System.Collections.Generic;
using System.Linq;
using FS_SSR_Combat.CustomWeapon;
using HarmonyLib;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(CompEquippable), "CompGetEquippedGizmosExtra")]
public static class Patch_CompEquippable_GetEquippedGizmos
{
	public static void Postfix(CompEquippable __instance, ref IEnumerable<Gizmo> __result)
	{
		if (__instance.parent is SSR_CustomWeapon sSR_CustomWeapon)
		{
			__result = __result.Concat(sSR_CustomWeapon.GetWornGizmos());
		}
	}
}
