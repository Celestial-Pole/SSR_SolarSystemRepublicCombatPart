using Verse;

namespace HJ_SSR.Weapons;

public class ModExtension_Verb_Shotgun : DefModExtension
{
	public int ShotgunPellets = 1;

	public ThingDef extraProjectile;

	public int extraProjectileCount = 1;

	public float spreadAngle = 10f;

	public float minSpreadDistance = 1f;
}
