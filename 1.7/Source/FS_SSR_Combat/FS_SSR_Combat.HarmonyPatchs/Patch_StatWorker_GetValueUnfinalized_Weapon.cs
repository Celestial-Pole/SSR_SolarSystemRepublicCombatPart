using FS_SSR_Combat.CustomWeapon;
using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
public static class Patch_StatWorker_GetValueUnfinalized_Weapon
{
	public static void Postfix(StatRequest req, StatDef ___stat, ref float __result)
	{
		if (req.HasThing && req.Thing is SSR_CustomWeapon sSR_CustomWeapon)
		{
			float statOffset = sSR_CustomWeapon.StatCache.GetStatOffset(___stat);
			if (!Mathf.Approximately(statOffset, 0f))
			{
				__result += statOffset;
			}
			float statFactor = sSR_CustomWeapon.StatCache.GetStatFactor(___stat);
			if (!Mathf.Approximately(statFactor, 1f))
			{
				__result *= statFactor;
			}
		}
	}
}
