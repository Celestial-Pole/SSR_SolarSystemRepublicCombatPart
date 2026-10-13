using System.Reflection;
using HarmonyLib;
using Verse;

namespace FS_TransWeapon.HarmonyPatchs;

[StaticConstructorOnStartup]
public class PatchMain
{
	static PatchMain()
	{
		Harmony harmony = new Harmony("FS_TransWeapon");
		harmony.PatchAll(Assembly.GetExecutingAssembly());
	}
}
