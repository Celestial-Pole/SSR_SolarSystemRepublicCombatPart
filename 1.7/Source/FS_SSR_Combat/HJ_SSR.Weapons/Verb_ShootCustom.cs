using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HJ_SSR.Weapons;

//按武器扩展配置执行散射、连发和弹药消耗。
public class Verb_ShootCustom : Verb_ShootAllowAllCast
{
	private int currenBurstRandomIndex;

	private Vector3 lastScatteredTargetPos = Vector3.zero;

	private bool isFirstShot = true;

	public DefModExtension_ShootUsingRandomProjectileBase DataRandProj => base.EquipmentCompSource.parent.def.GetModExtension<DefModExtension_ShootUsingRandomProjectileBase>();

	private ModExtension_Verb_Shotgun DataShotgun => base.EquipmentSource.def.GetModExtension<ModExtension_Verb_Shotgun>();

	private ModExtension_VerbNotUnderRoof DataNotUnderRoof => base.EquipmentSource.def.GetModExtension<ModExtension_VerbNotUnderRoof>();

	private DefModExtension_ShootUsingMechBattery DataMechBattery => base.EquipmentSource.def.GetModExtension<DefModExtension_ShootUsingMechBattery>();

	private ModExtension_RandomBurstBreak randomBurstBreak => base.EquipmentSource.def.GetModExtension<ModExtension_RandomBurstBreak>();

	private ModExtension_DropItemWhenFire DataDropItem => base.EquipmentSource.def.GetModExtension<ModExtension_DropItemWhenFire>();

	private ModExtension_OneUse DataOneUse => base.EquipmentSource.def.GetModExtension<ModExtension_OneUse>();

	private ModExtension_ProjOriginOffset DataOriginOffset => base.EquipmentSource.def.GetModExtension<ModExtension_ProjOriginOffset>();

	private ModExtension_MultiShot DataMultiShot => base.EquipmentSource.def.GetModExtension<ModExtension_MultiShot>();

	private CompSecondaryVerb compSecondaryVerb => base.EquipmentSource.TryGetComp<CompSecondaryVerb>();

	public override void WarmupComplete()
	{
		RandomizeProjectile();
		base.WarmupComplete();
		RandomizeBurstCount();
	}

	public override bool Available()
	{
		if (verbProps.consumeFuelPerBurst > 0f)
		{
			CompRefuelable compRefuelable = caster.TryGetComp<CompRefuelable>();
			if (compRefuelable != null && compRefuelable.Fuel < verbProps.consumeFuelPerBurst)
			{
				return false;
			}
		}
		if (AvailableNotUnderRoof() && AvailableMechBattery())
		{
			return base.Available();
		}
		return false;
	}

	public override void Notify_EquipmentLost()
	{
		base.Notify_EquipmentLost();
		if (state == VerbState.Bursting && burstShotsLeft < verbProps.burstShotCount)
		{
			TryCastShotOneUse();
		}
	}

	protected override bool TryCastShot()
	{
		if (DataRandProj != null && DataRandProj.randomWithinBurst)
		{
			RandomizeProjectile();
		}
		Vector3 drawPos = Caster.DrawPos;
		if (base.TryCastShot())
		{
			TryCastShotShotgun_new();
		}
		return false;
	}

	public bool AvailableNotUnderRoof()
	{
		if (DataNotUnderRoof != null && Caster.Position.Roofed(Caster.Map))
		{
			if (compSecondaryVerb != null && (!compSecondaryVerb.IsSecondaryVerbSelected || !DataNotUnderRoof.appliesInSecondaryMode))
			{
				if (!compSecondaryVerb.IsSecondaryVerbSelected)
				{
					return !DataNotUnderRoof.appliesInPrimaryMode;
				}
				return true;
			}
			return false;
		}
		return true;
	}

	public bool AvailableMechBattery()
	{
		Need_MechEnergy need_MechEnergy = CasterPawn?.needs.TryGetNeed<Need_MechEnergy>();
		if (need_MechEnergy != null)
		{
			return need_MechEnergy.CurLevel > DataMechBattery.energyConsumption;
		}
		return true;
	}

	public void RandomizeProjectile()
	{
		if (DataRandProj != null)
		{
			verbProps.defaultProjectile = DataRandProj.GetProjectile();
		}
	}

	public void RandomizeBurstCount()
	{
		if (randomBurstBreak != null)
		{
			burstShotsLeft += randomBurstBreak.randomBurst.RandomInRange;
		}
	}

	private void TryCastShotMechBattery()
	{
		if (ModLister.BiotechInstalled && DataMechBattery != null)
		{
			Need_MechEnergy need_MechEnergy = CasterPawn?.needs.TryGetNeed<Need_MechEnergy>();
			if (need_MechEnergy != null)
			{
				need_MechEnergy.CurLevel -= DataMechBattery.energyConsumption;
			}
		}
	}

	private void TryCastShotShotgun()
	{
		if (DataShotgun == null)
		{
			return;
		}
		if (DataShotgun.ShotgunPellets > 1)
		{
			isFirstShot = true;
			for (int i = 1; i < DataShotgun.ShotgunPellets; i++)
			{
				Vector3 vector = GetScatteredTarget(caster.DrawPos, currentTarget.Cell.ToVector3(), DataShotgun.spreadAngle, DataShotgun.minSpreadDistance).CenterVector3;
				if (!isFirstShot && Vector3.Distance(lastScatteredTargetPos, vector) < 0.1f)
				{
					float num = Rand.Range(0.5f, 1.5f);
					vector += new Vector3(Rand.Range(0f - num, num), 0f, Rand.Range(0f - num, num));
					IntVec3 c = vector.ToIntVec3();
					if (!c.InBounds(caster.Map))
					{
						vector = c.ClampInsideMap(caster.Map).ToVector3Shifted();
					}
				}
				lastScatteredTargetPos = vector;
				isFirstShot = false;
				LocalTargetInfo localTargetInfo = new LocalTargetInfo(vector.ToIntVec3());
				LocalTargetInfo localTargetInfo2 = currentTarget;
				currentTarget = localTargetInfo;
				bool flag = base.TryCastShot();
				currentTarget = localTargetInfo2;
				if (!flag)
				{
					return;
				}
			}
			verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
			verbProps.soundCastTail?.PlayOneShotOnCamera(caster.Map);
		}
		if (DataShotgun.extraProjectile != null && DataShotgun.extraProjectileCount > 0)
		{
			ThingDef defaultProjectile = verbProps.defaultProjectile;
			verbProps.defaultProjectile = DataShotgun.extraProjectile;
			for (int j = 0; j < DataShotgun.extraProjectileCount; j++)
			{
				base.TryCastShot();
			}
			verbProps.defaultProjectile = defaultProjectile;
		}
	}

	//按散射落点发射，并在开发模式绘制射线。
	private void TryCastShotShotgun_new()
	{
		if (DataShotgun == null || DataShotgun.ShotgunPellets <= 1)
		{
			return;
		}
		Vector3 drawPos = caster.DrawPos;
		Vector3 centerVector = currentTarget.CenterVector3;
		Vector3 normalized = (centerVector - drawPos).normalized;
		float num = verbProps.range / 2f;
		Vector3 vector = drawPos + normalized * num;
		Vector3 normalized2 = new Vector3(0f - normalized.z, 0f, normalized.x).normalized;
		int num2 = DataShotgun.ShotgunPellets - 1;
		float num3 = Mathf.Min(1.5f, verbProps.range / (float)num2);
		for (int i = 0; i < num2; i++)
		{
			float num4 = ((float)i - (float)num2 / 2f + 0.5f) * num3;
			Vector3 vector2 = vector + normalized2 * num4;
			IntVec3 intVec = vector2.ToIntVec3();
			if (!intVec.InBounds(caster.Map))
			{
				intVec = intVec.ClampInsideMap(caster.Map);
				vector2 = intVec.ToVector3Shifted();
			}
			LocalTargetInfo localTargetInfo = new LocalTargetInfo(intVec);
			LocalTargetInfo localTargetInfo2 = currentTarget;
			currentTarget = localTargetInfo;
			bool flag = base.TryCastShot();
			currentTarget = localTargetInfo2;
			if (DebugSettings.godMode)
			{
				GenDraw.DrawLineBetween(drawPos, vector2, SimpleColor.Yellow);
				GenDraw.DrawLineBetween(vector, vector2, SimpleColor.Blue);
			}
		}
		verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
		verbProps.soundCastTail?.PlayOneShotOnCamera(caster.Map);
	}

	//保留调试散射的弹丸与方向显示。
	private void TryCastShotShotgun_Debug()
	{
		if (DataShotgun == null || DataShotgun.ShotgunPellets <= 1)
		{
			return;
		}
		Vector3 drawPos = caster.DrawPos;
		Vector3 centerVector = currentTarget.CenterVector3;
		Vector3 normalized = (centerVector - drawPos).normalized;
		float num = verbProps.range / 2f;
		Vector3 vector = drawPos + normalized * num;
		Vector3 normalized2 = new Vector3(0f - normalized.z, 0f, normalized.x).normalized;
		int num2 = DataShotgun.ShotgunPellets - 1;
		float num3 = Mathf.Min(1.5f, verbProps.range / (float)num2);
		for (int i = 0; i < num2; i++)
		{
			float num4 = ((float)i - (float)num2 / 2f + 0.5f) * num3;
			Vector3 vector2 = vector + normalized2 * num4;
			IntVec3 intVec = vector2.ToIntVec3();
			if (!intVec.InBounds(caster.Map))
			{
				intVec = intVec.ClampInsideMap(caster.Map);
				vector2 = intVec.ToVector3Shifted();
			}
			LocalTargetInfo localTargetInfo = new LocalTargetInfo(intVec);
			LocalTargetInfo localTargetInfo2 = currentTarget;
			currentTarget = localTargetInfo;
			bool flag = base.TryCastShot();
			currentTarget = localTargetInfo2;
			if (DebugSettings.godMode)
			{
				GenDraw.DrawLineBetween(drawPos, vector2, SimpleColor.Yellow);
				GenDraw.DrawLineBetween(vector, vector2, SimpleColor.Blue);
			}
		}
		verbProps.soundCast?.PlayOneShot(new TargetInfo(caster.Position, caster.MapHeld));
		verbProps.soundCastTail?.PlayOneShotOnCamera(caster.Map);
	}

	private void TryCastShotMultiShot()
	{
		if (DataMultiShot == null || DataMultiShot.shotCount <= 1)
		{
			return;
		}
		float num = Projectile.projectile.SpeedTilesPerTick / (float)DataMultiShot.shotCount;
		for (int i = 1; i < DataMultiShot.shotCount; i++)
		{
			TryFindShootLineFromTo(caster.Position, currentTarget, out var resultingLine);
			if (base.EquipmentSource != null)
			{
				base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
				base.EquipmentSource.GetComp<CompApparelReloadable>()?.UsedOnce();
				base.EquipmentSource.GetComp<CompEquippableAbilityReloadable>()?.UsedOnce();
			}
			lastShotTick = Find.TickManager.TicksGame;
			Thing manningPawn = caster;
			Thing equipmentSource = base.EquipmentSource;
			CompMannable compMannable = caster.TryGetComp<CompMannable>();
			if (compMannable != null && compMannable.ManningPawn != null)
			{
				manningPawn = compMannable.ManningPawn;
				equipmentSource = caster;
			}
			Vector3 drawPos = caster.DrawPos;
			Projectile proj = (Projectile)GenSpawn.Spawn(Projectile, resultingLine.Source, caster.Map);
			if (verbProps.ForcedMissRadius > 0.5f)
			{
				float num2 = verbProps.ForcedMissRadius;
				if (manningPawn != null && manningPawn is Pawn pawn)
				{
					num2 *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
				}
				float num3 = VerbUtility.CalculateAdjustedForcedMiss(num2, currentTarget.Cell - caster.Position);
				if (num3 > 0.5f)
				{
					IntVec3 forcedMissTarget = GetForcedMissTarget(num3);
					if (forcedMissTarget != currentTarget.Cell)
					{
						ProjectileHitFlags projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
						if (Rand.Chance(0.5f))
						{
							projectileHitFlags = ProjectileHitFlags.All;
						}
						if (!canHitNonTargetPawnsNow)
						{
							projectileHitFlags &= ~ProjectileHitFlags.NonTargetPawns;
						}
						LaunchProjectileWithOffset(proj, num * (float)i, manningPawn, drawPos, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
						break;
					}
				}
			}
			ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
			Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
			ThingDef cov = randomCoverToMissInto?.def;
			if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
			{
				resultingLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget, Projectile.projectile.flyOverhead, caster.Map);
				ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
				if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
				{
					projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
				}
				LaunchProjectileWithOffset(proj, num * (float)i, manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, cov);
				break;
			}
			if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
			{
				ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
				if (canHitNonTargetPawnsNow)
				{
					projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
				}
				LaunchProjectileWithOffset(proj, num * (float)i, manningPawn, drawPos, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, cov);
				break;
			}
			ProjectileHitFlags projectileHitFlags4 = ProjectileHitFlags.IntendedTarget;
			if (canHitNonTargetPawnsNow)
			{
				projectileHitFlags4 |= ProjectileHitFlags.NonTargetPawns;
			}
			if (!currentTarget.HasThing || currentTarget.Thing.def.Fillage == FillCategory.Full)
			{
				projectileHitFlags4 |= ProjectileHitFlags.NonTargetWorld;
			}
			if (currentTarget.Thing != null)
			{
				LaunchProjectileWithOffset(proj, num * (float)i, manningPawn, drawPos, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource);
			}
			else
			{
				LaunchProjectileWithOffset(proj, num * (float)i, manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource);
			}
		}
	}

	protected virtual void LaunchProjectileWithOffset(Projectile Proj, float magnitude, Thing lcr, Vector3 origin, LocalTargetInfo uc, LocalTargetInfo ic, ProjectileHitFlags hitFlags, bool pff = false, Thing eq = null, ThingDef cov = null)
	{
		Vector3 vector = default(Vector3);
		vector = ((!uc.HasThing) ? (uc.Cell.ToVector3Shifted() - origin) : (uc.Thing.TrueCenter() - origin));
		vector.Normalize();
		vector *= magnitude;
		Proj.Launch(lcr, origin + vector, uc, currentTarget, hitFlags, pff, eq, cov);
		if (verbProps.consumeFuelPerShot > 0f)
		{
			caster.TryGetComp<CompRefuelable>()?.ConsumeFuel(verbProps.consumeFuelPerShot);
		}
		burstShotsLeft--;
		TryCastShotMechBattery();
		TryCastShotRandomBurstBreak();
		TryCastShotDropItemWhenFire();
	}

	private void TryCastShotRandomBurstBreak()
	{
		if (randomBurstBreak != null && Rand.Chance(randomBurstBreak.chance))
		{
			burstShotsLeft = 1;
		}
	}

	private void TryCastShotDropItemWhenFire()
	{
		if (DataDropItem != null)
		{
			Thing thing = ThingMaker.MakeThing(DataDropItem.Thingdef);
			if (CasterIsPawn && CasterPawn.Faction.IsPlayer && !DataDropItem.alwaysOnGround)
			{
				CasterPawn.inventory.innerContainer.TryAdd(thing);
			}
			else
			{
				thing.SetForbidden(value: true, warnOnFail: false);
			}
		}
	}

	private void TryCastShotOneUse()
	{
		if (burstShotsLeft > 1 || DataOneUse == null)
		{
			return;
		}
		if (base.EquipmentSource != null && !base.EquipmentSource.Destroyed)
		{
			base.EquipmentSource.Destroy();
		}
		if (CasterIsPawn && !CasterPawn.IsPlayerControlled)
		{
			Thing thing = GenClosest.ClosestThingReachable(CasterPawn.Position, CasterPawn.Map, ThingRequest.ForGroup(ThingRequestGroup.Weapon), PathEndMode.OnCell, TraverseParms.For(CasterPawn), 8f, (Thing x) => CasterPawn.CanReserve(x) && !x.IsBurning() && (!x.def.IsRangedWeapon || !CasterPawn.WorkTagIsDisabled(WorkTags.Shooting)), null, 0, 15);
			if (thing != null)
			{
				CasterPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Equip, thing), JobTag.Misc);
			}
		}
	}

	private LocalTargetInfo GetScatteredTarget(Vector3 origin, Vector3 target, float maxAngle, float minDistance)
	{
		Vector3 normalized = (target - origin).normalized;
		float num = Mathf.Max(Vector3.Distance(origin, target), minDistance);
		float angle = Rand.Range((0f - maxAngle) / 2f, maxAngle / 2f);
		Quaternion quaternion = Quaternion.AngleAxis(angle, Vector3.up);
		Vector3 vector = quaternion * normalized;
		Vector3 position = origin + vector * num;
		return GetActualTargetAt(position);
	}

	private LocalTargetInfo GetActualTargetAt(Vector3 position)
	{
		IntVec3 intVec = position.ToIntVec3();
		if (!intVec.InBounds(caster.Map))
		{
			return new LocalTargetInfo(intVec);
		}
		List<Thing> thingList = intVec.GetThingList(caster.Map);
		Thing thing = thingList.FirstOrDefault((Thing t) => t is Pawn pawn && pawn.RaceProps.IsFlesh && !pawn.Downed);
		if (thing == null)
		{
			return new LocalTargetInfo(intVec);
		}
		return new LocalTargetInfo(thing);
	}
}
