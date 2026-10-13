using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MYDE_Laser_Framework;

public class Comp_LaserData_Instant : ThingComp
{
	public CompProperties_LaserData_Instant Props => (CompProperties_LaserData_Instant)props;

	public void TakeDamageToTarget(Thing TargetThing, Thing Caster, Verb Verb)
	{
		Map map = Caster.Map;
		Vector3 drawPos = Caster.DrawPos;
		IntVec3 position = Caster.Position;
		Vector3 drawPos2 = TargetThing.DrawPos;
		IntVec3 intVec = drawPos2.ToIntVec3();
		DamageDef damageDef = Props.DamageDef;
		int damageNum = Props.DamageNum;
		float damageArmorPenetration = Props.DamageArmorPenetration;
		if (TargetThing == null || damageDef == null)
		{
			return;
		}
		if (!Props.IfCanDivide)
		{
			float angleFlat = (TargetThing.Position - Caster.Position).AngleFlat;
			BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(Caster, TargetThing, TargetThing, Verb.EquipmentSource.def, null, null);
			DamageInfo dinfo = new DamageInfo(damageDef, damageNum, damageArmorPenetration, angleFlat, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, TargetThing);
			TargetThing.TakeDamage(dinfo).AssociateWithLog(log);
			if (Props.IfSecondDamage)
			{
				DamageDef damageDef_B = Props.DamageDef_B;
				int damageNum_B = Props.DamageNum_B;
				float damageArmorPenetration_B = Props.DamageArmorPenetration_B;
				DamageInfo dinfo2 = new DamageInfo(damageDef_B, damageNum_B, damageArmorPenetration_B, angleFlat, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, TargetThing);
				TargetThing.TakeDamage(dinfo2).AssociateWithLog(log);
			}
			if (Props.IfCanScatter)
			{
				for (int i = 0; i < Props.ScatterNum; i++)
				{
					CellRect cellRect = CellRect.CenteredOn(intVec, Props.ScatterRadius);
					cellRect.ClipInsideMap(map);
					IntVec3 randomCell = cellRect.RandomCell;
					GenExplosion.DoExplosion(randomCell, map, Props.ScatterExplosionRadius, Props.ScatterExplosionDef, Caster, Props.ScatterExplosionDamage, Props.ScatterExplosionArmorPenetration, null, Verb.EquipmentSource.def, null, null, null, 0f, 0, null, null, 255, applyDamageToExplosionCellsNeighbors: false, null, 0f, 0);
					FleckMaker.ConnectingLine(drawPos2, randomCell.ToVector3Shifted(), Props.LaserFleck_ScatterLaser, map);
				}
			}
			if (Props.LaserLine_FleckDef != null)
			{
				float r = (float)Props.Color_Red / 255f;
				float g = (float)Props.Color_Green / 255f;
				float b = (float)Props.Color_Blue / 255f;
				float color_Alpha = Props.Color_Alpha;
				Vector3 vector = TargetThing.DrawPos - Caster.DrawPos;
				float x = vector.MagnitudeHorizontal();
				FleckCreationData dataStatic = FleckMaker.GetDataStatic(Caster.DrawPos + vector * 0.5f, map, Props.LaserLine_FleckDef);
				dataStatic.exactScale = new Vector3(x, 1f, 1f);
				dataStatic.rotation = Mathf.Atan2(0f - vector.z, vector.x) * 57.29578f;
				dataStatic.instanceColor = new Color(r, g, b, color_Alpha);
				map.flecks.CreateFleck(dataStatic);
			}
			if (Props.LaserFleck_Spark != null && Rand.Chance(Props.LaserFleck_Spark_Spawn_Chance))
			{
				float num = (drawPos2 - drawPos).AngleFlat();
				for (int j = 0; j < Props.LaserFleck_Spark_Num; j++)
				{
					float scale = Props.LaserFleck_Spark_Scale_Base + Rand.Range(0f - Props.LaserFleck_Spark_Scale_Deviation, Props.LaserFleck_Spark_Scale_Deviation);
					FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(drawPos2, map, Props.LaserFleck_Spark, scale);
					float num2 = num + Rand.Range(-30f, 30f);
					if (num2 > 180f)
					{
						num2 = num2 - 180f + -180f;
					}
					if (num2 < -180f)
					{
						num2 = num2 + 180f + 180f;
					}
					dataStatic2.velocityAngle = num2;
					dataStatic2.velocitySpeed = Rand.Range(5f, 10f);
					map.flecks.CreateFleck(dataStatic2);
				}
			}
			Props.SoundDef?.PlayOneShot(new TargetInfo(Caster.Position, Caster.MapHeld));
			if (!Props.IfCanScatter_II)
			{
				return;
			}
			List<IntVec3> pos_Square = MYDE_ModFront.GetPos_Square(intVec, Props.ScatterRadius_II, Props.ScatterRadius_II);
			List<Thing> list = new List<Thing>();
			List<Thing> list2 = new List<Thing>();
			Thing thing = null;
			for (int k = 0; k < pos_Square.Count; k++)
			{
				list.AddRange(pos_Square[k].GetThingList(map));
			}
			for (int l = 0; l < list.Count; l++)
			{
				if (list[l] == TargetThing)
				{
					continue;
				}
				if (list[l] is Pawn)
				{
					Pawn pawn = list[l] as Pawn;
					if (pawn.RaceProps.Animal && pawn.Faction == null)
					{
						list2.Add(list[l]);
					}
					else if (pawn.Faction.HostileTo(Caster.Faction) && !list2.Contains(list[l]))
					{
						list2.Add(list[l]);
					}
				}
				else if (list[l] is Building_Turret)
				{
					Building_Turret building_Turret = list[l] as Building_Turret;
					if (building_Turret.Faction.HostileTo(Caster.Faction) && !list2.Contains(list[l]))
					{
						list2.Add(list[l]);
					}
				}
			}
			List<Thing> list3 = new List<Thing>();
			if (list2.Count < 1)
			{
				return;
			}
			for (int m = 0; m < Props.ScatterNum_II; m++)
			{
				thing = list2.RandomElement();
				list2.Remove(thing);
				list3.Add(thing);
				thing = null;
				if (list2.Count <= 0)
				{
					break;
				}
			}
			for (int n = 0; n < list3.Count; n++)
			{
				float angleFlat2 = (list3[n].Position - TargetThing.Position).AngleFlat;
				BattleLogEntry_RangedImpact log2 = new BattleLogEntry_RangedImpact(Caster, list3[n], list3[n], Verb.EquipmentSource.def, null, null);
				DamageInfo dinfo3 = new DamageInfo(damageDef, (float)damageNum * Props.ScatterDamageFactor, damageArmorPenetration, angleFlat2, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, list3[n]);
				list3[n].TakeDamage(dinfo3).AssociateWithLog(log2);
				if (Props.IfScatterCanSecondDamage && Props.DamageDef_B != null)
				{
					DamageDef damageDef_B2 = Props.DamageDef_B;
					float amount = (float)Props.DamageNum_B * Props.ScatterDamageFactor;
					float damageArmorPenetration_B2 = Props.DamageArmorPenetration_B;
					DamageInfo dinfo4 = new DamageInfo(damageDef_B2, amount, damageArmorPenetration_B2, angleFlat2, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, list3[n]);
					list3[n].TakeDamage(dinfo4).AssociateWithLog(log);
				}
				if (Props.LaserFleck_ScatterLaser_II != null)
				{
					FleckMaker.ConnectingLine(TargetThing.Position.ToVector3Shifted(), list3[n].Position.ToVector3Shifted(), Props.LaserFleck_ScatterLaser_II, map);
				}
				if (Props.LaserFleck_Spark == null || !Rand.Chance(Props.LaserFleck_Spark_Spawn_Chance))
				{
					continue;
				}
				float num3 = (list3[n].Position.ToVector3Shifted() - TargetThing.Position.ToVector3Shifted()).AngleFlat();
				for (int num4 = 0; num4 < Props.LaserFleck_Spark_Num; num4++)
				{
					float scale2 = Props.LaserFleck_Spark_Scale_Base + Rand.Range(0f - Props.LaserFleck_Spark_Scale_Deviation, Props.LaserFleck_Spark_Scale_Deviation);
					FleckCreationData dataStatic3 = FleckMaker.GetDataStatic(list3[n].Position.ToVector3Shifted(), map, Props.LaserFleck_Spark, scale2);
					float num5 = num3 + Rand.Range(-30f, 30f);
					if (num5 > 180f)
					{
						num5 = num5 - 180f + -180f;
					}
					if (num5 < -180f)
					{
						num5 = num5 + 180f + 180f;
					}
					dataStatic3.velocityAngle = num5;
					dataStatic3.velocitySpeed = Rand.Range(5f, 10f);
					map.flecks.CreateFleck(dataStatic3);
				}
			}
		}
		else
		{
			if (!Props.IfCanDivide)
			{
				return;
			}
			List<IntVec3> pos_Square2 = MYDE_ModFront.GetPos_Square(intVec, Props.DivideRadius, Props.DivideRadius);
			List<Thing> list4 = new List<Thing>();
			List<Thing> list5 = new List<Thing>();
			Thing thing2 = null;
			for (int num6 = 0; num6 < pos_Square2.Count; num6++)
			{
				list4.AddRange(pos_Square2[num6].GetThingList(map));
			}
			for (int num7 = 0; num7 < list4.Count; num7++)
			{
				if (list4[num7] == TargetThing)
				{
					continue;
				}
				if (list4[num7] is Pawn)
				{
					Pawn pawn2 = list4[num7] as Pawn;
					if (pawn2.RaceProps.Animal && pawn2.Faction == null)
					{
						list5.Add(list4[num7]);
					}
					else if (pawn2.Faction.HostileTo(Caster.Faction) && !list5.Contains(list4[num7]))
					{
						list5.Add(list4[num7]);
					}
				}
				else if (list4[num7] is Building_Turret)
				{
					Building_Turret building_Turret2 = list4[num7] as Building_Turret;
					if (building_Turret2.Faction.HostileTo(Caster.Faction) && !list5.Contains(list4[num7]))
					{
						list5.Add(list4[num7]);
					}
				}
			}
			if (list5.Count >= 1)
			{
				thing2 = list5.RandomElement();
			}
			if (thing2 == null)
			{
				return;
			}
			float angleFlat3 = (thing2.Position - Caster.Position).AngleFlat;
			BattleLogEntry_RangedImpact log3 = new BattleLogEntry_RangedImpact(Caster, thing2, thing2, Verb.EquipmentSource.def, null, null);
			DamageInfo dinfo5 = new DamageInfo(damageDef, damageNum, damageArmorPenetration, angleFlat3, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, thing2);
			thing2.TakeDamage(dinfo5).AssociateWithLog(log3);
			if (Props.IfSecondDamage)
			{
				DamageDef damageDef_B3 = Props.DamageDef_B;
				int damageNum_B2 = Props.DamageNum_B;
				float damageArmorPenetration_B3 = Props.DamageArmorPenetration_B;
				DamageInfo dinfo6 = new DamageInfo(damageDef_B3, damageNum_B2, damageArmorPenetration_B3, angleFlat3, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, thing2);
				thing2.TakeDamage(dinfo6).AssociateWithLog(log3);
			}
			if (Props.IfCanScatter)
			{
				for (int num8 = 0; num8 < Props.ScatterNum; num8++)
				{
					CellRect cellRect2 = CellRect.CenteredOn(thing2.Position, Props.ScatterRadius);
					cellRect2.ClipInsideMap(map);
					IntVec3 randomCell2 = cellRect2.RandomCell;
					GenExplosion.DoExplosion(randomCell2, map, Props.ScatterExplosionRadius, Props.ScatterExplosionDef, Caster, Props.ScatterExplosionDamage, Props.ScatterExplosionArmorPenetration, null, Verb.EquipmentSource.def, null, null, null, 0f, 0, null, null, 255, applyDamageToExplosionCellsNeighbors: false, null, 0f, 0);
					FleckMaker.ConnectingLine(thing2.DrawPos, randomCell2.ToVector3Shifted(), Props.LaserFleck_ScatterLaser, map);
				}
			}
			if (Props.LaserLine_FleckDef != null)
			{
				float r2 = (float)Props.Color_Red / 255f;
				float g2 = (float)Props.Color_Green / 255f;
				float b2 = (float)Props.Color_Blue / 255f;
				float color_Alpha2 = Props.Color_Alpha;
				Vector3 vector2 = thing2.DrawPos - Caster.DrawPos;
				float x2 = vector2.MagnitudeHorizontal();
				FleckCreationData dataStatic4 = FleckMaker.GetDataStatic(Caster.DrawPos + vector2 * 0.5f, map, Props.LaserLine_FleckDef);
				dataStatic4.exactScale = new Vector3(x2, 1f, 1f);
				dataStatic4.rotation = Mathf.Atan2(0f - vector2.z, vector2.x) * 57.29578f;
				dataStatic4.instanceColor = new Color(r2, g2, b2, color_Alpha2);
				map.flecks.CreateFleck(dataStatic4);
			}
			if (Props.LaserFleck_Spark != null && Rand.Chance(Props.LaserFleck_Spark_Spawn_Chance))
			{
				float num9 = (thing2.DrawPos - drawPos).AngleFlat();
				for (int num10 = 0; num10 < Props.LaserFleck_Spark_Num; num10++)
				{
					float scale3 = Props.LaserFleck_Spark_Scale_Base + Rand.Range(0f - Props.LaserFleck_Spark_Scale_Deviation, Props.LaserFleck_Spark_Scale_Deviation);
					FleckCreationData dataStatic5 = FleckMaker.GetDataStatic(thing2.DrawPos, map, Props.LaserFleck_Spark, scale3);
					float num11 = num9 + Rand.Range(-30f, 30f);
					if (num11 > 180f)
					{
						num11 = num11 - 180f + -180f;
					}
					if (num11 < -180f)
					{
						num11 = num11 + 180f + 180f;
					}
					dataStatic5.velocityAngle = num11;
					dataStatic5.velocitySpeed = Rand.Range(5f, 10f);
					map.flecks.CreateFleck(dataStatic5);
				}
			}
			Props.SoundDef?.PlayOneShot(new TargetInfo(Caster.Position, Caster.MapHeld));
			if (!Props.IfCanScatter_II)
			{
				return;
			}
			List<IntVec3> pos_Square3 = MYDE_ModFront.GetPos_Square(thing2.Position, Props.ScatterRadius_II, Props.ScatterRadius_II);
			List<Thing> list6 = new List<Thing>();
			List<Thing> list7 = new List<Thing>();
			Thing thing3 = null;
			for (int num12 = 0; num12 < pos_Square3.Count; num12++)
			{
				list6.AddRange(pos_Square3[num12].GetThingList(map));
			}
			for (int num13 = 0; num13 < list6.Count; num13++)
			{
				if (list6[num13] == thing2)
				{
					continue;
				}
				if (list6[num13] is Pawn)
				{
					Pawn pawn3 = list6[num13] as Pawn;
					if (pawn3.RaceProps.Animal && pawn3.Faction == null)
					{
						list7.Add(list6[num13]);
					}
					else if (pawn3.Faction.HostileTo(Caster.Faction) && !list7.Contains(list6[num13]))
					{
						list7.Add(list6[num13]);
					}
				}
				else if (list6[num13] is Building_Turret)
				{
					Building_Turret building_Turret3 = list6[num13] as Building_Turret;
					if (building_Turret3.Faction.HostileTo(Caster.Faction) && !list7.Contains(list6[num13]))
					{
						list7.Add(list6[num13]);
					}
				}
			}
			List<Thing> list8 = new List<Thing>();
			if (list5.Count < 1)
			{
				return;
			}
			for (int num14 = 0; num14 < Props.ScatterNum_II; num14++)
			{
				thing3 = list7.RandomElement();
				list7.Remove(thing3);
				list8.Add(thing3);
				thing3 = null;
				if (list7.Count <= 0)
				{
					break;
				}
			}
			for (int num15 = 0; num15 < list8.Count; num15++)
			{
				float angleFlat4 = (list8[num15].Position - thing2.Position).AngleFlat;
				BattleLogEntry_RangedImpact log4 = new BattleLogEntry_RangedImpact(Caster, list8[num15], list8[num15], Verb.EquipmentSource.def, null, null);
				DamageInfo dinfo7 = new DamageInfo(damageDef, (float)damageNum * Props.ScatterDamageFactor, damageArmorPenetration, angleFlat4, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, list8[num15]);
				list8[num15].TakeDamage(dinfo7).AssociateWithLog(log4);
				if (Props.IfScatterCanSecondDamage && Props.DamageDef_B != null)
				{
					DamageDef damageDef_B4 = Props.DamageDef_B;
					float amount2 = (float)Props.DamageNum_B * Props.ScatterDamageFactor;
					float damageArmorPenetration_B4 = Props.DamageArmorPenetration_B;
					DamageInfo dinfo8 = new DamageInfo(damageDef_B4, amount2, damageArmorPenetration_B4, angleFlat4, Caster, null, Verb.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, list8[num15]);
					list8[num15].TakeDamage(dinfo8).AssociateWithLog(log3);
				}
				if (Props.LaserFleck_ScatterLaser_II != null)
				{
					FleckMaker.ConnectingLine(thing2.Position.ToVector3Shifted(), list8[num15].Position.ToVector3Shifted(), Props.LaserFleck_ScatterLaser_II, map);
				}
				if (Props.LaserFleck_Spark == null || !Rand.Chance(Props.LaserFleck_Spark_Spawn_Chance))
				{
					continue;
				}
				float num16 = (list8[num15].Position.ToVector3Shifted() - thing2.Position.ToVector3Shifted()).AngleFlat();
				for (int num17 = 0; num17 < Props.LaserFleck_Spark_Num; num17++)
				{
					float scale4 = Props.LaserFleck_Spark_Scale_Base + Rand.Range(0f - Props.LaserFleck_Spark_Scale_Deviation, Props.LaserFleck_Spark_Scale_Deviation);
					FleckCreationData dataStatic6 = FleckMaker.GetDataStatic(list8[num15].Position.ToVector3Shifted(), map, Props.LaserFleck_Spark, scale4);
					float num18 = num16 + Rand.Range(-30f, 30f);
					if (num18 > 180f)
					{
						num18 = num18 - 180f + -180f;
					}
					if (num18 < -180f)
					{
						num18 = num18 + 180f + 180f;
					}
					dataStatic6.velocityAngle = num18;
					dataStatic6.velocitySpeed = Rand.Range(5f, 10f);
					map.flecks.CreateFleck(dataStatic6);
				}
			}
		}
	}
}
