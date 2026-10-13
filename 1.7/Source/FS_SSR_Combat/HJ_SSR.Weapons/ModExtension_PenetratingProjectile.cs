using Verse;

namespace HJ_SSR.Weapons;

public class ModExtension_PenetratingProjectile : DefModExtension
{
	public float overpenDamageMulti = 0.5f;

	public float stopperDamageMulti = 1f;

	public float PenetrationFloorPercentage = 0.2f;

	public float penetrationPotentialMultiplier = 1f;

	public float postPenetrationDeviationAngle = 15f;

	public float minSearchRadius = 5f;

	public float maxSearchRadius = 20f;

	public float extraRange = 20f;

	public float buildingEquivalentMulti = 0.005f;

	public float armorEquivalentMulti = 1f;

	public float bodysizeEquivalentMulti = 0.1f;

	public float treeEquivalent = 0.1f;

	public float chunkEquivalent = 0.25f;

	public float bounceEquivalent = 0.2f;

	public bool straightPassThrough;

	public int maxPenetratingCount = -1;
}
