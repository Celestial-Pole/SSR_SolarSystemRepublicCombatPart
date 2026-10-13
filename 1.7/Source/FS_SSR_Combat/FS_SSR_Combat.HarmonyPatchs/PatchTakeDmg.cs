using HarmonyLib;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(Thing), "TakeDamage")]
public class PatchTakeDmg
{
	[HarmonyPrefix]
	public static void prefix(Thing __instance, DamageInfo dinfo)
	{
		if (!__instance.Destroyed)
		{
			TC_AbsoluteDamageAddon compAbsDmg = CombatUtilities.GetCompAbsDmg(__instance);
			if (compAbsDmg != null && !(compAbsDmg.Props.absDmgAmt <= 0f) && !compAbsDmg.Props.forceHit)
			{
				CombatUtilities.TakeAbsoluteDamage(dinfo.Instigator, __instance, compAbsDmg, dinfo);
			}
		}
	}
}
