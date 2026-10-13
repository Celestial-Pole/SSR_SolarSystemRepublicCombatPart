using Verse;

namespace HJ_SSR.Weapons;

public class ModExtension_RandomBurstBreak : DefModExtension
{
	public float chance = 0.08f;

	public IntRange randomBurst = new IntRange(0, 0);
}
