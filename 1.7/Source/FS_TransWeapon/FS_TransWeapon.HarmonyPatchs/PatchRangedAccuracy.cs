using HarmonyLib;
using RimWorld;
using Verse;

namespace FS_TransWeapon.HarmonyPatchs;

[HarmonyPatch(typeof(StatExtension), "GetStatValue")]
public class PatchRangedAccuracy
{
	[HarmonyPrefix]
	public static bool prefix(ref float __result, Thing thing, StatDef stat, bool applyPostProcess, int cacheStaleAfterTicks)
	{
		if (thing is MultiformWeapon multiformWeapon)
		{
			multiformWeapon.Initialize();
			if (multiformWeapon.cachedWeapons[multiformWeapon.currentWeapon].statBases.ContainsKey(stat))
			{
				__result = multiformWeapon.cachedWeapons[multiformWeapon.currentWeapon].statBases[stat];
				return false;
			}
		}
		return true;
	}
}
