using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace FS_TransWeapon.HarmonyPatchs;

[HarmonyPatch(typeof(Pawn), "GetGizmos")]
public class PatchWornGizmo
{
	[HarmonyPostfix]
	public static IEnumerable<Gizmo> postfix(IEnumerable<Gizmo> gizmos, Pawn __instance)
	{
		foreach (Gizmo gizmo in gizmos)
		{
			yield return gizmo;
		}
		if (!(__instance.equipment?.Primary is MultiformWeapon multiformWeapon))
		{
			yield break;
		}
		TC_MultiformSwitch comp = multiformWeapon.GetComp<TC_MultiformSwitch>();
		if (comp == null)
		{
			yield break;
		}
		foreach (Gizmo item in comp.CompGetGizmosExtra())
		{
			yield return item;
		}
	}
}
