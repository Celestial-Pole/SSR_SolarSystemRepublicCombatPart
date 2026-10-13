using System;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(AttackTargetFinder), "BestAttackTarget")]
public class Patch_IntellectTargetting
{
	[HarmonyPrefix]
	public static bool prefix(IAttackTarget __result, IAttackTargetSearcher searcher, TargetScanFlags flags, Predicate<Thing> validator = null, float minDist = 0f, float maxDist = 9999f, IntVec3 locus = default(IntVec3), float maxTravelRadiusFromLocus = float.MaxValue, bool canBashDoors = false, bool canTakeTargetsCloserThanEffectiveMinRange = true, bool canBashFences = false, bool onlyRanged = false)
	{
		return HarmonyPrefixRet.keepOriginal;
	}
}
