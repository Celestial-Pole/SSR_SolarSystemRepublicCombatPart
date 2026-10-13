using RimWorld;
using Verse;

namespace HJ_SSR.Weapons;

[DefOf]
public static class EffecterDefOf
{
	public static EffecterDef Effecter_SSR_PGHitTrunk;

	public static EffecterDef Effecter_SSR_PGHitTree;

	static EffecterDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(EffecterDefOf));
	}
}
