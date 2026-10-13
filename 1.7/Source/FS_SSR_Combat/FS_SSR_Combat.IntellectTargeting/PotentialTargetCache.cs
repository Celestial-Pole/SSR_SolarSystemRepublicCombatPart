using System;
using System.Collections.Generic;
using System.Collections.Generic.RedBlack;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.IntellectTargeting;

public class PotentialTargetCache
{
	private Map map;

	private Dictionary<StatDef, RedBlackTree<float, Pawn>> mapArmor = new Dictionary<StatDef, RedBlackTree<float, Pawn>>();

	private RedBlackTree<float, Pawn> mapDPH = new RedBlackTree<float, Pawn>();

	private RedBlackTree<float, Pawn> mapDPS = new RedBlackTree<float, Pawn>();

	private RedBlackTree<float, Pawn> mapHealth = new RedBlackTree<float, Pawn>();

	private RedBlackTree<float, Pawn> mapRange = new RedBlackTree<float, Pawn>();

	private RedBlackTree<float, Pawn> mapRating = new RedBlackTree<float, Pawn>();

	private Dictionary<Pawn, TargetRatingInfo> recordedPawn = new Dictionary<Pawn, TargetRatingInfo>();

	private const float MAP_INTERVAL = 0.0001f;

	private ConfigDef Config => ConfigDef.Config;

	private Dictionary<StatDef, float> Config_Armors => Config.ITF_ArmorOptions;

	public PotentialTargetCache(Map map)
	{
		this.map = map;
	}

	public void UpdatePawn(Pawn p)
	{
	}

	private void DeregisterTarget(Pawn p)
	{
		if (!recordedPawn.ContainsKey(p))
		{
			return;
		}
		Log.Message($"[SSR].IT trying dereg {p.Name}");
		TargetRatingInfo targetRatingInfo = recordedPawn[p];
		foreach (StatDef key in targetRatingInfo.ratingArmor.Keys)
		{
			mapArmor[key].Remove(targetRatingInfo.ratingArmor[key]);
		}
		mapDPH.Remove(targetRatingInfo.ratingDPH);
		mapDPS.Remove(targetRatingInfo.ratingDPS);
		mapRange.Remove(targetRatingInfo.attackRange);
		mapHealth.Remove(targetRatingInfo.health);
		mapRating.Remove(targetRatingInfo.threatRating);
		recordedPawn.Remove(p);
	}

	private void RegisterOrRefreshTarget(Pawn p)
	{
		float num = 0f;
		if (recordedPawn.ContainsKey(p))
		{
			TargetRatingInfo targetRatingInfo = recordedPawn[p];
			num = targetRatingInfo.threatRating;
			Log.Message($"[SSR].IT trying refresh {p.Name}");
			if (mapHealth.ContainsKey(targetRatingInfo.health))
			{
				mapHealth.Remove(targetRatingInfo.health);
			}
			else
			{
				Log.Error("[SSR] 智能炮塔未清理旧hp");
			}
			if (mapRating.ContainsKey(targetRatingInfo.threatRating))
			{
				mapRating.Remove(targetRatingInfo.threatRating);
			}
			else
			{
				Log.Error("[SSR] 只能炮塔未清理旧rt");
			}
			num -= targetRatingInfo.health * Config.ITF_Health;
		}
		else
		{
			Log.Message($"[SSR].IT trying reg {p.Name}");
			TargetRatingInfo targetRatingInfo = new TargetRatingInfo(p);
			foreach (StatDef key in Config_Armors.Keys)
			{
				float num2;
				for (num2 = ArmorRating(key, p); mapArmor[key].ContainsKey(num2); num2 += 0.0001f)
				{
				}
				num += num2 * Config_Armors[key];
				targetRatingInfo.ratingArmor.Add(key, num2);
			}
			double maxDPH;
			float atkRange;
			float num3 = (float)DPSCalculator(p, out maxDPH, out atkRange);
			float num4;
			for (num4 = (float)maxDPH; mapDPH.ContainsKey(num4); num4 += 0.0001f)
			{
			}
			mapDPH.Add(num4, p);
			num += num4 * Config.ITF_DPH;
			targetRatingInfo.ratingDPH = num4;
			for (; mapDPS.ContainsKey(num3); num3 += 0.0001f)
			{
			}
			mapDPS.Add(num3, p);
			num += num3 * Config.ITF_DPS;
			targetRatingInfo.ratingDPS = num3;
			for (; mapRange.ContainsKey(atkRange); atkRange += 0.0001f)
			{
			}
			mapRange.Add(atkRange, p);
			num += atkRange * Config.ITF_Range;
			targetRatingInfo.attackRange = atkRange;
			for (; mapRating.ContainsKey(num); num += 0.0001f)
			{
			}
			mapRating.Add(num, p);
			recordedPawn.Add(p, targetRatingInfo);
		}
		float num5;
		for (num5 = p.health.summaryHealth.SummaryHealthPercent; mapHealth.ContainsKey(num5); num5 += 0.0001f)
		{
		}
		mapHealth.Add(num5, p);
		recordedPawn[p].health = num5;
		num += num5 * Config.ITF_Health;
		recordedPawn[p].threatRating = num;
	}

	private static float ArmorRating(StatDef stat, Pawn pawn)
	{
		float num = 0f;
		float num2 = Mathf.Clamp01(pawn.GetStatValue(stat) / 2f);
		List<BodyPartRecord> allParts = pawn.RaceProps.body.AllParts;
		List<Apparel> list = pawn.apparel?.WornApparel;
		for (int i = 0; i < allParts.Count; i++)
		{
			float num3 = 1f - num2;
			if (list != null)
			{
				for (int j = 0; j < list.Count; j++)
				{
					if (list[j].def.apparel.CoversBodyPart(allParts[i]))
					{
						float num4 = Mathf.Clamp01(list[j].GetStatValue(stat) / 2f);
						num3 *= 1f - num4;
					}
				}
			}
			num += allParts[i].coverageAbs * (1f - num3);
		}
		return Mathf.Clamp(num * 2f, 0f, 2f);
	}

	private double DPSCalculator(Pawn p, out double maxDPH, out float atkRange)
	{
		double num = 0.0;
		double val = 0.0;
		double num2 = 0.0;
		maxDPH = 0.0;
		atkRange = 0f;
		Thing thing = p.equipment.PrimaryEq?.parent;
		if (thing == null)
		{
			return 0.0;
		}
		ThingDef def = thing.def;
		if (def != null)
		{
			if (def.tools != null)
			{
				float statValue = thing.GetStatValue(StatDefOf.MeleeWeapon_DamageMultiplier);
				double num3 = 0.0;
				foreach (Tool tool in def.tools)
				{
					num2 = 0.0;
					num2 += (double)tool.power;
					if (tool.extraMeleeDamages != null)
					{
						foreach (ExtraDamage extraMeleeDamage in tool.extraMeleeDamages)
						{
							num2 += (double)extraMeleeDamage.amount * Math.Min(1.0, extraMeleeDamage.chance);
						}
					}
					maxDPH = Math.Max(maxDPH, num2);
					num2 /= (double)tool.cooldownTime;
					num2 *= (double)tool.chanceFactor;
					num += num2;
					num3 += (double)tool.chanceFactor;
				}
				num = ((num3 != 0.0) ? (num / num3) : 0.0);
				num *= (double)statValue;
				maxDPH *= statValue;
			}
			if (def.Verbs != null && def.Verbs.Count > 0)
			{
				float statValue2 = thing.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier);
				VerbProperties verbProperties = def.Verbs[0];
				ProjectileProperties projectile = verbProperties.defaultProjectile.projectile;
				if (projectile != null)
				{
					atkRange = verbProperties.range;
					FieldInfo field = projectile.GetType().GetField("damageAmountBase", BindingFlags.Instance | BindingFlags.NonPublic);
					val = (int)field.GetValue(projectile);
					if (projectile.extraDamages != null)
					{
						foreach (ExtraDamage extraDamage in projectile.extraDamages)
						{
							val += (double)extraDamage.amount * Math.Min(extraDamage.chance, 1.0);
						}
					}
					maxDPH = Math.Max(maxDPH, val * (double)statValue2);
					double num4 = verbProperties.warmupTime;
					foreach (StatModifier statBasis in def.statBases)
					{
						if (statBasis.stat == StatDefOf.RangedWeapon_Cooldown)
						{
							num4 += (double)statBasis.value;
							break;
						}
					}
					if (verbProperties.burstShotCount > 1)
					{
						val *= (double)verbProperties.burstShotCount;
						num4 += (double)(verbProperties.ticksBetweenBurstShots / 60 * (verbProperties.burstShotCount - 1));
					}
					if (!(num4 <= 0.0))
					{
						val /= num4;
						val *= (double)statValue2;
						goto IL_031b;
					}
				}
				return num;
			}
		}
		goto IL_031b;
		IL_031b:
		return Math.Max(num, val);
	}
}
