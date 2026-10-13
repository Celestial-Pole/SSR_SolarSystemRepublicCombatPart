using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.HarmonyPatchs;

[HarmonyPatch(typeof(Projectile), "Launch", new Type[]
{
	typeof(Thing),
	typeof(Vector3),
	typeof(LocalTargetInfo),
	typeof(LocalTargetInfo),
	typeof(ProjectileHitFlags),
	typeof(bool),
	typeof(Thing),
	typeof(ThingDef)
})]
public class PatchProjectile
{
	[HarmonyPrefix]
	public static void Prefix(Thing launcher, Vector3 origin, ref LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ref ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
	{
		bool flag = false;
		ThingWithComps thingWithComps = equipment as ThingWithComps;
		TC_AbsoluteDamageAddon tC_AbsoluteDamageAddon = null;
		if (thingWithComps != null)
		{
			Pawn pawn = launcher as Pawn;
			tC_AbsoluteDamageAddon = thingWithComps.GetComp<TC_AbsoluteDamageAddon>();
			if (tC_AbsoluteDamageAddon != null)
			{
				flag = tC_AbsoluteDamageAddon.Researched;
			}
		}
		if (flag)
		{
			if (tC_AbsoluteDamageAddon.Props.absDmgAmt > 0f && intendedTarget.Thing != null)
			{
				Thing thing = intendedTarget.Thing;
				TC_AbsoluteDamageAddon comp = tC_AbsoluteDamageAddon;
				DamageDef damageType = tC_AbsoluteDamageAddon.Props.damageType;
				float absDmgAmt = tC_AbsoluteDamageAddon.Props.absDmgAmt;
				ThingDef def = thingWithComps.def;
				CombatUtilities.TakeAbsoluteDamage(launcher, thing, comp, new DamageInfo(damageType, absDmgAmt, 99f, -1f, launcher, null, def));
			}
			usedTarget = intendedTarget;
			hitFlags = ProjectileHitFlags.IntendedTarget;
		}
	}
}
