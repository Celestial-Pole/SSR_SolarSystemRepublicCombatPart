using System.Reflection;
using HarmonyLib;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[StaticConstructorOnStartup]
public class PatchMain
{
	static PatchMain()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		Harmony val = new Harmony("FS_SSRCombat");
		val.PatchAll(Assembly.GetExecutingAssembly());
	}
}
