using RimWorld;
using Verse;

namespace FS_SSR_Combat;

public class TCP_AbsoluteDamageAddon : CompProperties
{
	public bool forceHit;

	public float absDmgAmt = 10f;

	public bool overrideBallistic = true;

	public ResearchProjectDef researchReq;

	public DamageDef damageType = DamageDefOf.Bullet;

	public TCP_AbsoluteDamageAddon()
	{
		compClass = typeof(TC_AbsoluteDamageAddon);
	}
}
