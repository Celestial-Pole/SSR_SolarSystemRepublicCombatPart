using HarmonyLib;
using RimWorld;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

//装填查询使用当前弹种，目标为原版弹药定义的属性读取器。
[HarmonyPatch(typeof(CompApparelReloadable), nameof(CompApparelReloadable.AmmoDef), MethodType.Getter)]
public class Patch_ChosenAmmo
{
	//仅接管可切换弹种的组件，其余装备仍使用原版定义。
	[HarmonyPrefix]
	public static bool prefix(ref ThingDef __result, CompApparelReloadable __instance)
	{
		if (__instance is TC_MagAmmoChangable tC_MagAmmoChangable)
		{
			__result = tC_MagAmmoChangable.ChosenAmmo;
			return HarmonyPrefixRet.skipOriginal;
		}
		return HarmonyPrefixRet.keepOriginal;
	}
}
