using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MYDE_Laser_Framework;

public class Verb_Laser_Sustain : Verb
{
	public MoteDualAttached Mote;

	public FleckDef LaserFleck_End;

	public float LaserFleck_End_Scale_Base = 1f;

	public FleckDef LaserFleck_Spark;

	public float LaserFleck_Spark_Scale_Base = 1f;

	public float LaserFleck_Spark_Scale_Deviation;

	public int LaserFleck_Spark_Num = 1;

	public float LaserFleck_Spark_Spawn_Chance = 1f;

	public float StartPositionOffset_Range;

	public SoundDef SoundDef;

	public DamageDef DamageDef = DamageDefOf.Cut;

	public int DamageNum = 1;

	public float DamageArmorPenetration;

	public bool IfSecondDamage;

	public DamageDef DamageDef_B = DamageDefOf.Cut;

	public int DamageNum_B = 1;

	public float DamageArmorPenetration_B;

	public bool IfCanScatter;

	public int ScatterNum = 1;

	public int ScatterRadius = 1;

	public DamageDef ScatterExplosionDef = DamageDefOf.Bomb;

	public int ScatterExplosionDamage = 1;

	public float ScatterExplosionRadius = 1f;

	public float ScatterExplosionArmorPenetration = 1f;

	public int ScatterTick;

	public int ScatterTickMax = 1;

	public FleckDef LaserFleck_ScatterLaser;

	protected override int ShotsPerBurst => verbProps.burstShotCount;

	public Vector3 TargetPosition_Vector3 => base.CurrentTarget.CenterVector3;

	public override float? AimAngleOverride
	{
		get
		{
			if (state != VerbState.Bursting)
			{
				return null;
			}
			return (TargetPosition_Vector3 - caster.DrawPos).AngleFlat();
		}
	}

	public override void BurstingTick()
	{
		Map map = caster.Map;
		Vector3 vector = Caster.DrawPos;
		IntVec3 position = Caster.Position;
		Vector3 targetPosition_Vector = TargetPosition_Vector3;
		IntVec3 cell = targetPosition_Vector.ToIntVec3();
		if (StartPositionOffset_Range != 0f)
		{
			float angle = (vector - targetPosition_Vector).AngleFlat();
			vector = MYDE_ModFront.GetVector3_By_AngleFlat(vector, StartPositionOffset_Range, angle);
		}
		Vector3 offsetA = vector - position.ToVector3Shifted();
		Vector3 offsetB = targetPosition_Vector - cell.ToVector3Shifted();
		if (Mote != null)
		{
			Mote.UpdateTargets(new TargetInfo(position, map), new TargetInfo(cell, map), offsetA, offsetB);
			Mote.Maintain();
		}
		if (LaserFleck_End != null)
		{
			FleckCreationData dataStatic = FleckMaker.GetDataStatic(targetPosition_Vector, map, LaserFleck_End, LaserFleck_End_Scale_Base);
			map.flecks.CreateFleck(dataStatic);
		}
		if (!IfCanScatter)
		{
			return;
		}
		ScatterTick++;
		if (ScatterTick >= ScatterTickMax)
		{
			ScatterTick = 0;
			for (int i = 0; i < ScatterNum; i++)
			{
				CellRect cellRect = CellRect.CenteredOn(base.CurrentTarget.Cell, ScatterRadius);
				cellRect.ClipInsideMap(map);
				IntVec3 randomCell = cellRect.RandomCell;
				GenExplosion.DoExplosion(randomCell, map, ScatterExplosionRadius, ScatterExplosionDef, caster, ScatterExplosionDamage, ScatterExplosionArmorPenetration, null, base.EquipmentSource.def, null, null, null, 0f, 0, null, null, 255, applyDamageToExplosionCellsNeighbors: false, null, 0f, 0);
				FleckMaker.ConnectingLine(base.CurrentTarget.Thing.DrawPos, randomCell.ToVector3Shifted(), LaserFleck_ScatterLaser, map);
			}
		}
	}

	public override void WarmupComplete()
	{
		base.WarmupComplete();
		burstShotsLeft = ShotsPerBurst;
		state = VerbState.Bursting;
		CompProperties_LaserData_Sustain props = base.EquipmentSource.TryGetComp<Comp_LaserData_Sustain>().Props;
		ThingDef laserLine_MoteDef = props.LaserLine_MoteDef;
		float r = (float)props.Color_Red / 255f;
		float g = (float)props.Color_Green / 255f;
		float b = (float)props.Color_Blue / 255f;
		float color_Alpha = props.Color_Alpha;
		if (laserLine_MoteDef != null)
		{
			Mote = MoteMaker.MakeInteractionOverlay(laserLine_MoteDef, caster, new TargetInfo(TargetPosition_Vector3.ToIntVec3(), caster.Map));
			Mote.instanceColor = new Color(r, g, b, color_Alpha);
		}
		if (currentTarget.Thing is Pawn { Downed: false, IsColonyMech: false } pawn && CasterIsPawn && CasterPawn.skills != null)
		{
			float num = (pawn.HostileTo(caster) ? 170f : 20f);
			float num2 = verbProps.AdjustedFullCycleTime(this, CasterPawn);
			CasterPawn.skills.Learn(SkillDefOf.Shooting, num * num2);
		}
		LaserFleck_End = props.LaserFleck_End;
		LaserFleck_End_Scale_Base = props.LaserFleck_End_Scale_Base;
		LaserFleck_Spark = props.LaserFleck_Spark;
		LaserFleck_Spark_Scale_Base = props.LaserFleck_Spark_Scale_Base;
		LaserFleck_Spark_Scale_Deviation = props.LaserFleck_Spark_Scale_Deviation;
		LaserFleck_Spark_Num = props.LaserFleck_Spark_Num;
		LaserFleck_Spark_Spawn_Chance = props.LaserFleck_Spark_Spawn_Chance;
		StartPositionOffset_Range = props.StartPositionOffset_Range;
		SoundDef = props.SoundDef;
		DamageDef = props.DamageDef;
		DamageNum = props.DamageNum;
		DamageArmorPenetration = props.DamageArmorPenetration;
		IfSecondDamage = props.IfSecondDamage;
		DamageDef_B = props.DamageDef_B;
		DamageNum_B = props.DamageNum_B;
		DamageArmorPenetration_B = props.DamageArmorPenetration_B;
		IfCanScatter = props.IfCanScatter;
		if (IfCanScatter)
		{
			ScatterNum = props.ScatterNum;
			ScatterRadius = props.ScatterRadius;
			ScatterExplosionDef = props.ScatterExplosionDef;
			ScatterExplosionDamage = props.ScatterExplosionDamage;
			ScatterExplosionRadius = props.ScatterExplosionRadius;
			ScatterExplosionArmorPenetration = props.ScatterExplosionArmorPenetration;
			ScatterTickMax = props.ScatterTickMax;
			LaserFleck_ScatterLaser = props.LaserFleck_ScatterLaser;
		}
		TryCastNextBurstShot();
		props.SoundDef?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
	}

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
		lastShotTick = Find.TickManager.TicksGame;
		TakeDamageToTarget(base.CurrentTarget.Thing);
		return true;
	}

	private void TakeDamageToTarget(Thing TargetThing)
	{
		Map map = caster.Map;
		Vector3 drawPos = Caster.DrawPos;
		IntVec3 position = Caster.Position;
		Vector3 targetPosition_Vector = TargetPosition_Vector3;
		IntVec3 intVec = targetPosition_Vector.ToIntVec3();
		DamageDef damageDef = DamageDef;
		int damageNum = DamageNum;
		float damageArmorPenetration = DamageArmorPenetration;
		Map map2 = caster.Map;
		if (TargetThing != null && damageDef != null)
		{
			float angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;
			BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(caster, TargetThing, currentTarget.Thing, base.EquipmentSource.def, null, null);
			DamageInfo dinfo = new DamageInfo(damageDef, damageNum, damageArmorPenetration, angleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing);
			TargetThing.TakeDamage(dinfo).AssociateWithLog(log);
			if (IfSecondDamage)
			{
				DamageDef damageDef_B = DamageDef_B;
				int damageNum_B = DamageNum_B;
				float damageArmorPenetration_B = DamageArmorPenetration_B;
				DamageInfo dinfo2 = new DamageInfo(damageDef_B, damageNum_B, damageArmorPenetration_B, angleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing);
				TargetThing.TakeDamage(dinfo2).AssociateWithLog(log);
			}
		}
		if (LaserFleck_Spark == null || !Rand.Chance(LaserFleck_Spark_Spawn_Chance))
		{
			return;
		}
		float num = (targetPosition_Vector - drawPos).AngleFlat();
		for (int i = 0; i < LaserFleck_Spark_Num; i++)
		{
			float scale = LaserFleck_Spark_Scale_Base + Rand.Range(0f - LaserFleck_Spark_Scale_Deviation, LaserFleck_Spark_Scale_Deviation);
			FleckCreationData dataStatic = FleckMaker.GetDataStatic(targetPosition_Vector, map, LaserFleck_Spark, scale);
			float num2 = num + Rand.Range(-30f, 30f);
			if (num2 > 180f)
			{
				num2 = num2 - 180f + -180f;
			}
			if (num2 < -180f)
			{
				num2 = num2 + 180f + 180f;
			}
			dataStatic.velocityAngle = num2;
			dataStatic.velocitySpeed = Rand.Range(5f, 10f);
			map.flecks.CreateFleck(dataStatic);
		}
	}
}
