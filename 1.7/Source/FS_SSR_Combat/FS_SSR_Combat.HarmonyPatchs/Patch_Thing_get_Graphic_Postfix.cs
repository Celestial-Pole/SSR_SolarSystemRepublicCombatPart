using FS_SSR_Combat.CustomWeapon;
using HarmonyLib;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(Thing), "get_Graphic")]
public static class Patch_Thing_get_Graphic_Postfix
{
	[HarmonyPostfix]
	public static void Postfix(Thing __instance, ref Graphic __result)
	{
		if (__instance is SSR_CustomWeapon sSR_CustomWeapon)
		{
			__result = sSR_CustomWeapon.GetModularGraphic();
		}
	}
}
