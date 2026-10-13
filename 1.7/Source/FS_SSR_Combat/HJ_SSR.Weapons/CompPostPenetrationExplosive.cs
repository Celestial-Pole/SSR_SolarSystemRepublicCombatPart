using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HJ_SSR.Weapons;

public class CompPostPenetrationExplosive : ThingComp
{
	public CompProperties_PostPenetrationExplosive Props => props as CompProperties_PostPenetrationExplosive;

	public virtual void Explode(Thing instigator, Map map, ThingDef weapon, Thing intendedTarget, List<Thing> ignoredThings = null)
	{
		if (Props.explosiveRadius > 0f && parent.def != null)
		{
			Thing instigator2 = ((instigator == null || (instigator.HostileTo(parent.Faction) && parent.Faction != Faction.OfPlayer)) ? parent : instigator);
			IntVec3 positionHeld = parent.PositionHeld;
			float explosiveRadius = Props.explosiveRadius;
			DamageDef explosiveDamageType = Props.explosiveDamageType;
			int damageAmountBase = Props.damageAmountBase;
			float armorPenetrationBase = Props.armorPenetrationBase;
			SoundDef explosionSound = Props.explosionSound;
			ThingDef def = instigator.def;
			ThingDef postExplosionSpawnThingDef = Props.postExplosionSpawnThingDef;
			float postExplosionSpawnChance = Props.postExplosionSpawnChance;
			int postExplosionSpawnThingCount = Props.postExplosionSpawnThingCount;
			GasType? postExplosionGasType = Props.postExplosionGasType;
			bool applyDamageToExplosionCellsNeighbors = Props.applyDamageToExplosionCellsNeighbors;
			ThingDef preExplosionSpawnThingDef = Props.preExplosionSpawnThingDef;
			float preExplosionSpawnChance = Props.preExplosionSpawnChance;
			int preExplosionSpawnThingCount = Props.preExplosionSpawnThingCount;
			float chanceToStartFire = Props.chanceToStartFire;
			bool damageFalloff = Props.damageFalloff;
			float? direction = null;
			FloatRange? affectedAngle = null;
			bool doVisualEffects = Props.doVisualEffects;
			float propagationSpeed = Props.propagationSpeed;
			GenExplosion.DoExplosion(positionHeld, map, explosiveRadius, explosiveDamageType, instigator2, damageAmountBase, armorPenetrationBase, explosionSound, weapon, def, intendedTarget, postExplosionSpawnThingDef, postExplosionSpawnChance, postExplosionSpawnThingCount, postExplosionGasType, null, 255, applyDamageToExplosionCellsNeighbors, preExplosionSpawnThingDef, preExplosionSpawnChance, preExplosionSpawnThingCount, chanceToStartFire, damageFalloff, direction, ignoredThings, affectedAngle, doVisualEffects, propagationSpeed);
			parent.Destroy();
		}
	}
}
