using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FS_SSR_Combat.IntellectTargeting;

public class TargetRatingInfo
{
	public Pawn parent;

	public Dictionary<StatDef, float> ratingArmor = new Dictionary<StatDef, float>();

	public float ratingDPS;

	public float ratingDPH;

	public float attackRange;

	public float health;

	public float threatRating;

	public TargetRatingInfo(Pawn pawn)
	{
		parent = pawn;
	}
}
