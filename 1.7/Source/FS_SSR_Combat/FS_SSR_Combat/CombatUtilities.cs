using Verse;

namespace FS_SSR_Combat;

public static class CombatUtilities
{
	public static void TakeAbsoluteDamage(Thing caster, Thing victim, TC_AbsoluteDamageAddon comp, DamageInfo dinfo)
	{
		if (victim.Destroyed)
		{
			return;
		}
		DamageInfo damageInfo = new DamageInfo(dinfo);
		float absDmgAmt = comp.Props.absDmgAmt;
		damageInfo.SetAmount(absDmgAmt);
		damageInfo.Def = comp.DamageType;
		if (victim.def.useHitPoints)
		{
			victim.HitPoints -= (int)absDmgAmt;
			if (victim.HitPoints <= 0)
			{
				victim.HitPoints = 0;
				victim.Kill(damageInfo);
			}
		}
		damageInfo.Def.Worker.Apply(damageInfo, victim);
	}

	public static TC_AbsoluteDamageAddon GetCompAbsDmg(Thing t)
	{
		if (!(t is ThingWithComps thingWithComps))
		{
			return null;
		}
		return thingWithComps.GetComp<TC_AbsoluteDamageAddon>();
	}
}
