using Verse;

namespace HJ_SSR.Weapons;

public abstract class DefModExtension_ShootUsingRandomProjectileBase : DefModExtension
{
	public bool randomWithinBurst;

	public abstract ThingDef GetProjectile();
}
