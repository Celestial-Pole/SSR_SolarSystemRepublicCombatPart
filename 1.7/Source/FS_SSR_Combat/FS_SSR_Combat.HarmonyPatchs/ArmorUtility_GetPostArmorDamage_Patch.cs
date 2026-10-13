using System;
using System.Linq;
using System.Text;
using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.CustomArmor;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(ArmorUtility), "GetPostArmorDamage")]
public static class ArmorUtility_GetPostArmorDamage_Patch
{
	public static bool Prefix(Pawn pawn, ref float amount, float armorPenetration, BodyPartRecord part, ref DamageDef damageDef, ref float __result, out bool deflectedByMetalArmor, out bool diminishedByMetalArmor)
	{
		deflectedByMetalArmor = false;
		diminishedByMetalArmor = false;
		SSR_CustomArmor sSR_CustomArmor = pawn.apparel?.WornApparel.OfType<SSR_CustomArmor>().FirstOrDefault();
		if (sSR_CustomArmor == null || part == null || amount <= 0f)
		{
			return true;
		}
		StatDef statDef = damageDef.armorCategory?.armorRatingStat;
		if (statDef == null)
		{
			return true;
		}
		try
		{
			StringBuilder stringBuilder = (Prefs.DevMode ? new StringBuilder() : null);
			stringBuilder?.AppendLine("--- [CAF] Damage Calculation for " + pawn.LabelShort + " ---");
			stringBuilder?.AppendLine($"Hit Part: {part.LabelCap}, Initial Damage: {amount:F1} ({damageDef.defName})");
			sSR_CustomArmor.TryGetCoveringAttachments(part, out var coveringAttachments);
			stringBuilder?.AppendLine($"Covering attachments found: {coveringAttachments?.Count ?? 0}");
			float num = amount;
			DamageDef damageDef2 = damageDef;
			if (coveringAttachments != null)
			{
				foreach (IAttachable item in coveringAttachments)
				{
					if (!(num <= 0f))
					{
						float singleAttachmentArmorRating = sSR_CustomArmor.GetSingleAttachmentArmorRating(statDef, item);
						singleAttachmentArmorRating = Mathf.Min(singleAttachmentArmorRating, 2f);
						stringBuilder?.AppendLine($"  - Applying armor from '{item.def.LabelCap}' (Priority: {item.def.apparelProperties?.coveragePriority ?? 0}, Armor: {singleAttachmentArmorRating:P0})");
						float num2 = num;
						num = ApplyArmor(num, armorPenetration, singleAttachmentArmorRating, pawn, damageDef2, out var deflected, out var diminished);
						stringBuilder?.AppendLine($"    > Damage reduced from {num2:F1} to {num:F1}");
						if (deflected)
						{
							deflectedByMetalArmor = true;
						}
						if (diminished)
						{
							diminishedByMetalArmor = true;
						}
						if (diminished && damageDef2.armorCategory == DamageArmorCategoryDefOf.Sharp)
						{
							damageDef2 = DamageDefOf.Blunt;
						}
						continue;
					}
					break;
				}
			}
			float statValue = sSR_CustomArmor.GetStatValue(statDef);
			if (num > 0f && statValue > 0f)
			{
				stringBuilder?.AppendLine($"  - Applying armor from base apparel '{sSR_CustomArmor.LabelCap}' ({statValue:P0})");
				statValue = Mathf.Min(statValue, 2f);
				float num3 = num;
				num = ApplyArmor(num, armorPenetration, statValue, pawn, damageDef2, out var deflected2, out var diminished2);
				stringBuilder?.AppendLine($"    > Damage reduced from {num3:F1} to {num:F1}");
				if (deflected2)
				{
					deflectedByMetalArmor = true;
				}
				if (diminished2)
				{
					diminishedByMetalArmor = true;
				}
			}
			float num4 = num;
			stringBuilder?.AppendLine($"-> Total damage penetrated armor stats: {num4:F1}");
			if (num4 > 0f && coveringAttachments != null)
			{
				DamageInfo dinfo = new DamageInfo(damageDef2, num4, armorPenetration, -1f, pawn, part);
				__result = sSR_CustomArmor.DistributeDamageToAttachments(num4, dinfo, coveringAttachments, stringBuilder);
			}
			else
			{
				__result = num4;
			}
			stringBuilder?.AppendLine($"--- [CAF] Final Pawn Damage: {__result:F1} ---");
			if (stringBuilder != null)
			{
				Log.Message(stringBuilder.ToString());
			}
			return false;
		}
		catch (Exception arg)
		{
			Log.Error($"[CAF] Error in GetPostArmorDamage patch: {arg}");
			return true;
		}
	}

	private static float ApplyArmor(float damageAmount, float armorPen, float armorRating, Pawn pawn, DamageDef damageDef, out bool deflected, out bool diminished)
	{
		deflected = false;
		diminished = false;
		if (armorRating <= 0f)
		{
			return damageAmount;
		}
		float num = Mathf.Max(armorRating - armorPen, 0f);
		float result = damageAmount;
		if (Rand.Value < num * 0.5f)
		{
			result = 0f;
			deflected = true;
		}
		else if (Rand.Value < num)
		{
			result = GenMath.RoundRandom(damageAmount * 0.5f);
			diminished = true;
		}
		return result;
	}
}
