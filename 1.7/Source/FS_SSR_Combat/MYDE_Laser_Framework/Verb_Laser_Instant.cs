using RimWorld;
using UnityEngine;
using Verse;

namespace MYDE_Laser_Framework;

public class Verb_Laser_Instant : Verb
{
	protected override int ShotsPerBurst => verbProps.burstShotCount;

	public Vector3 TargetPosition_Vector3 => base.CurrentTarget.CenterVector3;

	protected override bool TryCastShot()
	{
		if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
		{
			return false;
		}
		ShootLine resultingLine;
		bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
		if (verbProps.stopBurstWithoutLos && !flag)
		{
			return false;
		}
		base.EquipmentSource?.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
		for (int i = 0; i < base.EquipmentSource.AllComps.Count; i++)
		{
			if (base.EquipmentSource.AllComps[i] is Comp_LaserData_Instant)
			{
				Comp_LaserData_Instant comp_LaserData_Instant = base.EquipmentSource.AllComps[i] as Comp_LaserData_Instant;
				comp_LaserData_Instant.TakeDamageToTarget(base.CurrentTarget.Thing, Caster, this);
			}
		}
		return true;
	}

	public override void WarmupComplete()
	{
		base.WarmupComplete();
		if (currentTarget.Thing is Pawn { Downed: false, IsColonyMech: false } pawn && CasterIsPawn && CasterPawn.skills != null)
		{
			float num = (pawn.HostileTo(caster) ? 170f : 20f);
			float num2 = verbProps.AdjustedFullCycleTime(this, CasterPawn);
			CasterPawn.skills.Learn(SkillDefOf.Shooting, num * num2);
		}
	}
}
