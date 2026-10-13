using HarmonyLib;
using Verse;

namespace FS_TransWeapon.HarmonyPatchs;

[HarmonyPatch(typeof(VerbTracker), "CreateVerbTargetCommand")]
public class PatchVerbTargetGizmo
{
	[HarmonyPostfix]
	public static void postfix(Thing ownerThing, Verb verb, ref Command_VerbTarget __result)
	{
		if (ownerThing is MultiformWeapon multiformWeapon)
		{
			__result.icon = multiformWeapon.ActualDef.uiIcon;
		}
	}
}
