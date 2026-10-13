using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HJ_SSR.Weapons;

public class Verb_ShootAllowAllCast : Verb_Shoot
{
	private static List<IntVec3> tempLeanShootSources = new List<IntVec3>();

	private static List<IntVec3> tempDestList = new List<IntVec3>();

	protected override bool TryCastShot()
	{
		if (caster == null || caster.Map == null)
		{
			Log.Message("[HJ_SSR] TryCastShot: 射手无效或无地图。射手: " + (caster?.ToString() ?? "null"));
			return false;
		}
		if (currentTarget.Thing == null || currentTarget.Thing.Map == null)
		{
			Log.Message("[HJ_SSR] TryCastShot: 目标无效或无地图。目标: " + (currentTarget.Thing?.ToString() ?? "null"));
			return false;
		}
		if (currentTarget.Thing.Map != caster.Map)
		{
			Log.Message("[HJ_SSR] TryCastShot: 射手和目标不在同一地图。射手地图: " + (caster.Map?.ToString() ?? "null") + "，目标地图: " + (currentTarget.Thing.Map?.ToString() ?? "null"));
			return false;
		}
		if (currentTarget.HasThing)
		{
			_ = currentTarget.Thing.Map;
			_ = caster.Map;
		}
		ThingDef projectile = Projectile;
		if (projectile == null)
		{
			return false;
		}
		if (!TryFindShootLineFromTo(caster.Position, currentTarget, out var resultingLine))
		{
			ShootLine shootLine = new ShootLine(caster.Position, currentTarget.Cell);
			resultingLine = shootLine;
		}
		if (verbProps.stopBurstWithoutLos)
		{
		}
		if (base.EquipmentSource != null)
		{
			base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
			base.EquipmentSource.GetComp<CompApparelVerbOwner_Charged>()?.UsedOnce();
		}
		lastShotTick = Find.TickManager.TicksGame;
		Thing manningPawn = caster;
		Thing equipmentSource = base.EquipmentSource;
		CompMannable compMannable = caster.TryGetComp<CompMannable>();
		if (compMannable?.ManningPawn != null)
		{
			manningPawn = compMannable.ManningPawn;
			equipmentSource = caster;
		}
		Vector3 drawPos = caster.DrawPos;
		Projectile projectile2 = (Projectile)GenSpawn.Spawn(projectile, resultingLine.Source, caster.Map);
		if (verbProps.ForcedMissRadius > 0.5f)
		{
			float num = verbProps.ForcedMissRadius;
			if (manningPawn is Pawn pawn)
			{
				num *= verbProps.GetForceMissFactorFor(equipmentSource, pawn);
			}
			float num2 = VerbUtility.CalculateAdjustedForcedMiss(num, currentTarget.Cell - caster.Position);
			if (num2 > 0.5f)
			{
				IntVec3 forcedMissTarget = GetForcedMissTarget(num2);
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
					projectile2.Launch(manningPawn, drawPos, forcedMissTarget, currentTarget, projectileHitFlags, preventFriendlyFire, equipmentSource);
					return true;
				}
			}
		}
		ShotReport shotReport = ShotReport.HitReportFor(caster, this, currentTarget);
		Thing randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
		ThingDef targetCoverDef = randomCoverToMissInto?.def;
		if (verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
		{
			bool flag = projectile2?.def?.projectile != null && projectile2.def.projectile.flyOverhead;
			ProjectileHitFlags projectileHitFlags2 = ProjectileHitFlags.NonTargetWorld;
			if (Rand.Chance(0.5f) && canHitNonTargetPawnsNow)
			{
				projectileHitFlags2 |= ProjectileHitFlags.NonTargetPawns;
			}
			projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags2, preventFriendlyFire, equipmentSource, targetCoverDef);
			return true;
		}
		if (currentTarget.Thing != null && currentTarget.Thing.def.CanBenefitFromCover && !Rand.Chance(shotReport.PassCoverChance))
		{
			ProjectileHitFlags projectileHitFlags3 = ProjectileHitFlags.NonTargetWorld;
			if (canHitNonTargetPawnsNow)
			{
				projectileHitFlags3 |= ProjectileHitFlags.NonTargetPawns;
			}
			projectile2.Launch(manningPawn, drawPos, randomCoverToMissInto, currentTarget, projectileHitFlags3, preventFriendlyFire, equipmentSource, targetCoverDef);
			return true;
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
			projectile2.Launch(manningPawn, drawPos, currentTarget, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
		}
		else
		{
			projectile2.Launch(manningPawn, drawPos, resultingLine.Dest, currentTarget, projectileHitFlags4, preventFriendlyFire, equipmentSource, targetCoverDef);
		}
		return true;
	}

	public bool CustomTryFindShootLineFromTo(IntVec3 root, LocalTargetInfo targ, out ShootLine resultingLine, bool ignoreRange = false)
	{
		if (targ.HasThing && targ.Thing.Map != caster.Map)
		{
			resultingLine = default(ShootLine);
			return false;
		}
		if (verbProps.IsMeleeAttack || EffectiveRange <= 1.42f)
		{
			resultingLine = new ShootLine(root, targ.Cell);
			return ReachabilityImmediate.CanReachImmediate(root, targ, caster.Map, PathEndMode.Touch, null);
		}
		CellRect occupiedRect = (targ.HasThing ? targ.Thing.OccupiedRect() : CellRect.SingleCell(targ.Cell));
		if (!ignoreRange && OutOfRange(root, targ, occupiedRect))
		{
			resultingLine = new ShootLine(root, targ.Cell);
			return false;
		}
		if (!verbProps.requireLineOfSight)
		{
			resultingLine = new ShootLine(root, targ.Cell);
			return true;
		}
		IntVec3 goodDest;
		if (CasterIsPawn)
		{
			if (CanHitFromCellIgnoringRange(root, targ, out goodDest))
			{
				resultingLine = new ShootLine(root, goodDest);
				return true;
			}
			ShootLeanUtility.LeanShootingSourcesFromTo(root, occupiedRect.ClosestCellTo(root), caster.Map, tempLeanShootSources);
			for (int i = 0; i < tempLeanShootSources.Count; i++)
			{
				IntVec3 intVec = tempLeanShootSources[i];
				if (CanHitFromCellIgnoringRange(intVec, targ, out goodDest))
				{
					resultingLine = new ShootLine(intVec, goodDest);
					return true;
				}
			}
		}
		else
		{
			foreach (IntVec3 item in caster.OccupiedRect())
			{
				if (CanHitFromCellIgnoringRange(item, targ, out goodDest))
				{
					resultingLine = new ShootLine(item, goodDest);
					return true;
				}
			}
		}
		resultingLine = new ShootLine(root, targ.Cell);
		return false;
	}

	private bool CanHitFromCellIgnoringRange(IntVec3 sourceCell, LocalTargetInfo targ, out IntVec3 goodDest)
	{
		if (targ.Thing != null)
		{
			if (targ.Thing.Map != caster.Map)
			{
				goodDest = IntVec3.Invalid;
				return false;
			}
			ShootLeanUtility.CalcShootableCellsOf(tempDestList, targ.Thing, sourceCell);
			for (int i = 0; i < tempDestList.Count; i++)
			{
				if (CanHitCellFromCellIgnoringRange(sourceCell, tempDestList[i], targ.Thing.def.Fillage == FillCategory.Full))
				{
					goodDest = tempDestList[i];
					return true;
				}
			}
		}
		else if (CanHitCellFromCellIgnoringRange(sourceCell, targ.Cell))
		{
			goodDest = targ.Cell;
			return true;
		}
		goodDest = IntVec3.Invalid;
		return false;
	}

	private bool CanHitCellFromCellIgnoringRange(IntVec3 sourceSq, IntVec3 targetLoc, bool includeCorners = false)
	{
		if (verbProps.mustCastOnOpenGround && (!targetLoc.Standable(caster.Map) || caster.Map.thingGrid.CellContains(targetLoc, ThingCategory.Pawn)))
		{
			return false;
		}
		if (verbProps.requireLineOfSight)
		{
			if (!includeCorners)
			{
				if (!GenSight.LineOfSight(sourceSq, targetLoc, caster.Map, skipFirstCell: true))
				{
					return false;
				}
			}
			else if (!GenSight.LineOfSightToEdges(sourceSq, targetLoc, caster.Map, skipFirstCell: true))
			{
				return false;
			}
		}
		return true;
	}
}
