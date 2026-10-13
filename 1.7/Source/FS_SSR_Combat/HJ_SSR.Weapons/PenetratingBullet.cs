using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HJ_SSR.Weapons;

public class PenetratingBullet : Bullet
{
	public bool ShouldTerminate;

	private Vector3 initialVector;

	private Vector3 trueOrigin;

	private readonly HashSet<IntVec3> checkedCells = new HashSet<IntVec3>();

	private int dbgTickIntervals;

	private int dbgPathChecks;

	private int dbgStepSamples;

	private int dbgUniqueCells;

	private int dbgThingCandidates;

	private int dbgCanHitPasses;

	private int dbgDestroyedOutOfBounds;

	private int dbgSubTicks;

	private int dbgFriendlySkips;

	private int dbgFilteredTransientSkips;

	private string dbgDestroyReason = "unknown";

	private const float SimpleHitCost = 0.05f;

	public float MaxPenetratingPower;

	public float ticksToDetonation = 100f;

	public bool fuseActivated;

	public int PenetratingCount = -1;

	private List<Thing> thingList = new List<Thing>();

	public static bool canDestroyNow = true;

	public List<Thing> attackedThings = new List<Thing>();

	public float penetratingPower;

	private float weaponDamageMultiplier => equipment?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;

	public float PenetratingPowerBase => ((MaxPenetratingPower > 0f) ? MaxPenetratingPower : penetratingPower) * PenetrationFloorPercentage;

	public override float ArmorPenetration
	{
		get
		{
			if (Extension != null)
			{
				return weaponDamageMultiplier * (penetratingPower / MaxPenetratingPower);
			}
			return def.projectile.GetArmorPenetration(equipment);
		}
	}

	private ModExtension_PenetratingProjectile Extension => def.GetModExtension<ModExtension_PenetratingProjectile>();

	public override int DamageAmount
	{
		get
		{
			if (!ShouldTerminate)
			{
				return def.projectile.GetDamageAmount(overpenDamageMulti, equipment);
			}
			return def.projectile.GetDamageAmount(StopperDamageMulti, equipment);
		}
	}

	public float overpenDamageMulti
	{
		get
		{
			if (Extension != null)
			{
				return Extension.overpenDamageMulti;
			}
			return 0.5f;
		}
	}

	public float StopperDamageMulti
	{
		get
		{
			if (Extension != null)
			{
				return Extension.stopperDamageMulti;
			}
			return 1f;
		}
	}

	public float PenetrationFloorPercentage
	{
		get
		{
			if (Extension != null)
			{
				return Extension.PenetrationFloorPercentage;
			}
			return 0.2f;
		}
	}

	public float BuildingEquivalentMulti
	{
		get
		{
			if (Extension != null)
			{
				return Extension.buildingEquivalentMulti;
			}
			return 0.005f;
		}
	}

	public float BodysizeEquivalentMulti
	{
		get
		{
			if (Extension != null)
			{
				return Extension.bodysizeEquivalentMulti;
			}
			return 0.1f;
		}
	}

	public float TreeEquivalent
	{
		get
		{
			if (Extension != null)
			{
				return Extension.treeEquivalent;
			}
			return 0.1f;
		}
	}

	public float ChunkEquivalent
	{
		get
		{
			if (Extension != null)
			{
				return Extension.chunkEquivalent;
			}
			return 0.25f;
		}
	}

	public float BounceEquivalent
	{
		get
		{
			if (Extension != null)
			{
				return Extension.bounceEquivalent;
			}
			return 0.2f;
		}
	}

	public float ArmorEquivalentMulti
	{
		get
		{
			if (Extension != null)
			{
				return Extension.armorEquivalentMulti;
			}
			return 1f;
		}
	}

	public float MinSearchRadius
	{
		get
		{
			if (Extension != null)
			{
				return Extension.minSearchRadius;
			}
			return 5f;
		}
	}

	public float ExtraRange
	{
		get
		{
			if (Extension != null)
			{
				return Extension.extraRange;
			}
			return 5f;
		}
	}

	public float MaxSearchRadius
	{
		get
		{
			if (Extension != null)
			{
				return Extension.maxSearchRadius;
			}
			return 20f;
		}
	}

	public float PostPenetrationDeviationAngle
	{
		get
		{
			if (Extension != null)
			{
				return Extension.postPenetrationDeviationAngle;
			}
			return 5f;
		}
	}

	public bool straightPassThrough
	{
		get
		{
			if (Extension != null)
			{
				return Extension.straightPassThrough;
			}
			return false;
		}
	}

	public CompPostPenetrationExplosive compExplosive => this.TryGetComp<CompPostPenetrationExplosive>();

	public int maxPenetratingCount
	{
		get
		{
			if (Extension != null)
			{
				return Extension.maxPenetratingCount;
			}
			return -1;
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);
		if (def.projectile.damageDef.armorCategory == DamageArmorCategoryDefOf.Sharp)
		{
			penetratingPower = def.projectile.GetArmorPenetration(equipment);
			if (Extension != null)
			{
				penetratingPower *= Extension.penetrationPotentialMultiplier;
			}
		}
		MaxPenetratingPower = penetratingPower;
		if (compExplosive != null)
		{
			ticksToDetonation = compExplosive.Props.wickTicks.RandomInRange;
		}
	}

	public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
	{
		base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
		trueOrigin = origin;
		initialVector = (usedTarget.CenterVector3 - origin).normalized;
		float valueOrDefault = (equipmentDef.Verbs[0].range + Extension?.extraRange).GetValueOrDefault();
		Vector3 vect = origin + initialVector * valueOrDefault;
		destination = vect.ToIntVec3().ClampInsideMap(launcher.Map).ToVector3Shifted();
		ticksToImpact = (int)base.StartingTicksToImpact;
		PenetratingCount = maxPenetratingCount;
		dbgTickIntervals = 0;
		dbgPathChecks = 0;
		dbgStepSamples = 0;
		dbgUniqueCells = 0;
		dbgThingCandidates = 0;
		dbgCanHitPasses = 0;
		dbgDestroyedOutOfBounds = 0;
		dbgSubTicks = 0;
		dbgFriendlySkips = 0;
		dbgFilteredTransientSkips = 0;
		dbgDestroyReason = "alive";
	}

	protected override void Tick()
	{
		if (penetratingPower <= PenetratingPowerBase || base.Map == null)
		{
			dbgDestroyReason = $"penetration_end p:{penetratingPower:F3} base:{PenetratingPowerBase:F3} mapNull:{base.Map == null}";
			canDestroyNow = true;
			Destroy();
			return;
		}
		base.Tick();
		if (compExplosive != null && fuseActivated)
		{
			ticksToDetonation -= 1f;
			if (ticksToDetonation < 0f || penetratingPower <= PenetratingPowerBase)
			{
				compExplosive.Explode(this, base.Map, equipmentDef, intendedTarget.Thing);
			}
		}
	}

	protected override void TickInterval(int delta)
	{
		dbgTickIntervals++;
		foreach (ThingComp allComp in base.AllComps)
		{
			allComp.CompTickInterval(delta);
		}
		for (int i = 0; i < delta; i++)
		{
			dbgSubTicks++;
			lifetime--;
			if (landed || base.Destroyed)
			{
				break;
			}
			Vector3 exactPosition = ExactPosition;
			ticksToImpact--;
			if (!ExactPosition.InBounds(base.Map))
			{
				dbgDestroyedOutOfBounds++;
				dbgDestroyReason = $"out_of_bounds exact:{ExactPosition} delta:{delta}";
				ticksToImpact++;
				base.Position = ExactPosition.ToIntVec3();
				canDestroyNow = true;
				Destroy();
				break;
			}
			Vector3 exactPosition2 = ExactPosition;
			if (CheckForFreeInterceptBetween(exactPosition, exactPosition2))
			{
				canDestroyNow = true;
				Destroy();
				break;
			}
			base.Position = ExactPosition.ToIntVec3();
			if (ticksToImpact <= 0)
			{
				if (base.DestinationCell.InBounds(base.Map))
				{
					base.Position = base.DestinationCell;
				}
				ImpactSomething();
				break;
			}
		}
	}

	private bool CheckForFreeInterceptBetween(Vector3 lastExactPos, Vector3 newExactPos)
	{
		dbgPathChecks++;
		if (lastExactPos == newExactPos)
		{
			return false;
		}
		List<Thing> list = base.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
		for (int i = 0; i < list.Count; i++)
		{
			if (list[i].TryGetComp<CompProjectileInterceptor>().CheckIntercept(this, lastExactPos, newExactPos))
			{
				Impact(null, blockedByShield: true);
				return true;
			}
		}
		IntVec3 intVec = lastExactPos.ToIntVec3();
		IntVec3 intVec2 = newExactPos.ToIntVec3();
		if (intVec2 == intVec)
		{
			return false;
		}
		if (!intVec.InBounds(base.Map) || !intVec2.InBounds(base.Map))
		{
			return false;
		}
		if (intVec2.AdjacentToCardinal(intVec))
		{
			return CheckForFreeIntercept(intVec2);
		}
		if (intVec.x == intVec2.x || intVec.z == intVec2.z)
		{
			List<IntVec3> list2 = new List<IntVec3>(GenSight.BresenhamCellsBetween(intVec, intVec2));
			for (int j = 0; j < list2.Count; j++)
			{
				IntVec3 c = list2[j];
				if (c.InBounds(base.Map))
				{
					dbgStepSamples++;
					dbgUniqueCells++;
					if (CheckForFreeIntercept(c))
					{
						return true;
					}
				}
			}
			return false;
		}
		Vector3 vect = lastExactPos;
		Vector3 v = newExactPos - lastExactPos;
		float num = v.MagnitudeHorizontal();
		if (num < 0.0001f)
		{
			return false;
		}
		Vector3 vector = v.normalized * 0.2f;
		int num2 = Mathf.CeilToInt(num / 0.2f) + 2;
		checkedCells.Clear();
		int num3 = 0;
		IntVec3 intVec3;
		do
		{
			vect += vector;
			intVec3 = vect.ToIntVec3();
			dbgStepSamples++;
			if (!checkedCells.Contains(intVec3))
			{
				dbgUniqueCells++;
				if (CheckForFreeIntercept(intVec3))
				{
					return true;
				}
				checkedCells.Add(intVec3);
			}
			num3++;
			if (num3 > num2)
			{
				if (!checkedCells.Contains(intVec2) && CheckForFreeIntercept(intVec2))
				{
					return true;
				}
				return false;
			}
		}
		while (intVec3 != intVec2);
		return false;
	}

	private bool CheckForFreeIntercept(IntVec3 c)
	{
		thingList = c.GetThingList(base.Map);
		for (int i = 0; i < thingList.Count; i++)
		{
			Thing thing = thingList[i];
			dbgThingCandidates++;
			if (!thing.def.useHitPoints && !(thing is Pawn))
			{
				dbgFilteredTransientSkips++;
			}
			else
			{
				if (!CanHit(thing) || attackedThings.Contains(thing))
				{
					continue;
				}
				float num;
				if (thing is Pawn pawn)
				{
					num = 0.5f * Mathf.Clamp(pawn.BodySize, 0.1f, 2f);
					if (pawn.GetPosture() != PawnPosture.Standing && (origin - destination).MagnitudeHorizontalSquared() >= 20.25f)
					{
						num *= 0.5f;
					}
					if (launcher != null && pawn.Faction != null && launcher.Faction != null && !pawn.Faction.HostileTo(launcher.Faction))
					{
						num *= VerbUtility.InterceptChanceFactorFromDistance(origin, base.Position);
					}
				}
				else
				{
					num = 1.5f * thing.def.fillPercent;
				}
				if (!Rand.Chance(num))
				{
					continue;
				}
				if (IsFriendlyBuildingOrPawn(thing))
				{
					dbgFriendlySkips++;
					continue;
				}
				dbgCanHitPasses++;
				attackedThings.Add(thing);
				ApplyHitDamage(thing, c);
				if (!(thing is Plant plant) || IsTreePlant(plant))
				{
					penetratingPower -= 0.05f;
					if (penetratingPower <= PenetratingPowerBase)
					{
						ShouldTerminate = true;
						return true;
					}
				}
			}
		}
		return false;
	}

	protected override void Impact(Thing hitThing, bool blockedByShield = false)
	{
		landed = false;
	}

	protected override void ImpactSomething()
	{
		dbgDestroyReason = "impact_disabled_path_debug";
		canDestroyNow = true;
		Destroy();
	}

	public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
	{
		if (canDestroyNow)
		{
			base.Destroy(mode);
		}
	}

	private void LogPathCell(IntVec3 c)
	{
		Log.Message($"[SSR_PB_PATH][{thingIDNumber}] tick={Find.TickManager.TicksGame} cell={c} p={penetratingPower:F2}");
	}

	private void LogHitCell(IntVec3 c, Thing thing)
	{
		Log.Message(string.Format("[SSR_PB_HITCELL][{0}] tick={1} cell={2} thing={3} id={4} p={5:F2}", thingIDNumber, Find.TickManager.TicksGame, c, thing?.def?.defName ?? "null", thing?.thingIDNumber ?? (-1), penetratingPower));
	}

	private static bool IsTreePlant(Plant plant)
	{
		if (plant == null)
		{
			return false;
		}
		if (plant.def.ingestible == null || plant.def.ingestible.foodType != FoodTypeFlags.Tree)
		{
			return plant.def.defName == "BurnedTree";
		}
		return true;
	}

	private bool IsFriendlyBuildingOrPawn(Thing thing)
	{
		if (thing == null || launcher == null || launcher.Faction == null || thing.Faction == null)
		{
			return false;
		}
		if (thing is Building building)
		{
			if (building.def.defName.Contains("Wall"))
			{
				return false;
			}
			return !thing.Faction.HostileTo(launcher.Faction);
		}
		if (thing is Pawn)
		{
			return !thing.Faction.HostileTo(launcher.Faction);
		}
		return false;
	}

	private void ApplyHitDamage(Thing hitThing, IntVec3 impactCell)
	{
		if (hitThing == null)
		{
			return;
		}
		if (hitThing is Pawn pawn)
		{
			EffecterDef damageEffecter = pawn.RaceProps.FleshType.damageEffecter;
			if (damageEffecter != null)
			{
				if (pawn.health.woundedEffecter != null && pawn.health.woundedEffecter.def != damageEffecter)
				{
					pawn.health.woundedEffecter.Cleanup();
				}
				pawn.health.woundedEffecter = damageEffecter.Spawn();
				pawn.health.woundedEffecter.Trigger(pawn, launcher ?? pawn);
			}
		}
		else if (hitThing is Plant plant && IsTreePlant(plant))
		{
			TargetInfo targetInfo = new TargetInfo(plant.Position, base.Map);
			Effecter effecter = EffecterDefOf.Effecter_SSR_PGHitTrunk.Spawn();
			effecter.Trigger(targetInfo, targetInfo);
			effecter.Cleanup();
			if (!plant.LeaflessNow && plant.def.defName != "Plant_SaguaroCactus")
			{
				Effecter effecter2 = EffecterDefOf.Effecter_SSR_PGHitTree.Spawn();
				effecter2.Trigger(targetInfo, targetInfo);
				effecter2.Cleanup();
			}
		}
		BattleLogEntry_RangedImpact battleLogEntry_RangedImpact = new BattleLogEntry_RangedImpact(launcher, hitThing, intendedTarget.Thing, equipmentDef, def, targetCoverDef);
		Find.BattleLog.Add(battleLogEntry_RangedImpact);
		OriginalNotifyImpact(hitThing, base.Map, impactCell);
		bool instigatorGuilty = !(launcher is Pawn pawn2) || !pawn2.Drafted;
		DamageInfo dinfo = new DamageInfo(def.projectile.damageDef, DamageAmount, ArmorPenetration, ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
		dinfo.SetWeaponQuality(equipmentQuality);
		hitThing.TakeDamage(dinfo).AssociateWithLog(battleLogEntry_RangedImpact);
		(hitThing as Pawn)?.stances?.stagger.Notify_BulletImpact(this);
		if (def.projectile.extraDamages == null)
		{
			return;
		}
		foreach (ExtraDamage extraDamage in def.projectile.extraDamages)
		{
			if (Rand.Chance(extraDamage.chance))
			{
				DamageInfo dinfo2 = new DamageInfo(extraDamage.def, extraDamage.amount, extraDamage.AdjustedArmorPenetration(), ExactRotation.eulerAngles.y, launcher, null, equipmentDef, DamageInfo.SourceCategory.ThingOrUnknown, intendedTarget.Thing, instigatorGuilty);
				hitThing.TakeDamage(dinfo2).AssociateWithLog(battleLogEntry_RangedImpact);
			}
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Collections.Look(ref attackedThings, "attackedThings", LookMode.Reference);
		Scribe_Values.Look(ref penetratingPower, "penetratingPower", 0f);
	}

	public void OriginalNotifyImpact(Thing hitThing, Map map, IntVec3 position)
	{
		BulletImpactData impactData = new BulletImpactData
		{
			bullet = this,
			hitThing = hitThing,
			impactPosition = position
		};
		hitThing?.Notify_BulletImpactNearby(impactData);
		int num = 9;
		for (int i = 0; i < num; i++)
		{
			IntVec3 c = position + GenRadial.RadialPattern[i];
			if (!c.InBounds(map))
			{
				continue;
			}
			List<Thing> list = c.GetThingList(map);
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j] != hitThing)
				{
					list[j].Notify_BulletImpactNearby(impactData);
				}
			}
		}
	}
}
