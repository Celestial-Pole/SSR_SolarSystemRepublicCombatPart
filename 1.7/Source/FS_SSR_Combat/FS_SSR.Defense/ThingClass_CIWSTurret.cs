using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR.Defense;

public class ThingClass_CIWSTurret : Turret_SSR
{
	private delegate void GetDrawPositionAndRotationDelegate(Skyfaller skyfaller, ref Vector3 drawLoc, out float extraRotation);

	private delegate float GetArcHeightFactorDelegate(Projectile projectile);

	private delegate float GetDistanceCoveredFractionArcDelegate(Projectile projectile);

	private static GetDrawPositionAndRotationDelegate getDrawPositionAndRotationDelegate;

	private static GetArcHeightFactorDelegate getArcHeightFactorDelegate;

	private static GetDistanceCoveredFractionArcDelegate getDistanceCoveredFractionArcDelegate;

	public override Vector3 TargetRelativePostion
	{
		get
		{
			if (CurrentTarget.HasThing)
			{
				if (CurrentTarget.Thing is Skyfaller skyfaller)
				{
					if (getDrawPositionAndRotationDelegate == null)
					{
						MethodInfo method = typeof(Skyfaller).GetMethod("GetDrawPositionAndRotation", BindingFlags.Instance | BindingFlags.NonPublic);
						getDrawPositionAndRotationDelegate = (GetDrawPositionAndRotationDelegate)Delegate.CreateDelegate(typeof(GetDrawPositionAndRotationDelegate), method);
					}
					Vector3 groundLoc = skyfaller.TrueCenter();
					Vector3 drawLoc = skyfaller.DrawPos;
					getDrawPositionAndRotationDelegate(skyfaller, ref drawLoc, out var _);
					return TransfromFlyingTarget(groundLoc, drawLoc);
				}
				if (CurrentTarget.Thing is Projectile projectile)
				{
					if (getArcHeightFactorDelegate == null)
					{
						MethodInfo method2 = typeof(Projectile).GetMethod("get_ArcHeightFactor", BindingFlags.Instance | BindingFlags.NonPublic);
						getArcHeightFactorDelegate = (GetArcHeightFactorDelegate)Delegate.CreateDelegate(typeof(GetArcHeightFactorDelegate), method2);
					}
					if (getDistanceCoveredFractionArcDelegate == null)
					{
						MethodInfo method3 = typeof(Projectile).GetMethod("get_DistanceCoveredFractionArc", BindingFlags.Instance | BindingFlags.NonPublic);
						getDistanceCoveredFractionArcDelegate = (GetDistanceCoveredFractionArcDelegate)Delegate.CreateDelegate(typeof(GetDistanceCoveredFractionArcDelegate), method3);
					}
					Vector3 drawPos = projectile.DrawPos;
					Vector3 drawLoc2 = drawPos + getArcHeightFactorDelegate(projectile) * GenMath.InverseParabola(getDistanceCoveredFractionArcDelegate(projectile)) * Vector3.forward;
					return TransfromFlyingTarget(drawPos, drawLoc2);
				}
			}
			return base.TargetRelativePostion;
		}
	}

	public Vector3 TransfromFlyingTarget(Vector3 groundLoc, Vector3 drawLoc)
	{
		groundLoc.x = drawLoc.x;
		Vector3 normalized = base.RootTransform.InverseTransformVector(Vector2.up).normalized;
		groundLoc = base.RootTransform.InverseTransformPoint(groundLoc);
		groundLoc -= normalized * groundLoc.y / normalized.y;
		normalized = base.RootTransform.TransformVector(Vector2.up).normalized;
		groundLoc = base.RootTransform.TransformPoint(groundLoc);
		drawLoc = groundLoc + normalized * (drawLoc.z - groundLoc.z) / normalized.z;
		return base.RootTransform.InverseTransformPoint(drawLoc);
	}

	public override LocalTargetInfo TryFindNewTarget()
	{
		List<Thing> list = base.MapHeld.spawnedThings.Where((Thing x) => x is Skyfaller || (x is Projectile projectile && projectile.def.projectile.flyOverhead && projectile.Launcher.HostileTo(this))).ToList();
		if (list.NullOrEmpty())
		{
			return base.TryFindNewTarget();
		}
		LocalTargetInfo localTargetInfo = list[0];
		float num = (localTargetInfo.CenterVector3 - DrawPos).sqrMagnitude;
		for (int num2 = 1; num2 < list.Count; num2++)
		{
			LocalTargetInfo localTargetInfo2 = list[num2];
			float sqrMagnitude = (localTargetInfo2.CenterVector3 - DrawPos).sqrMagnitude;
			if (sqrMagnitude < num)
			{
				localTargetInfo = localTargetInfo2;
				num = sqrMagnitude;
			}
		}
		if (base.AttackVerb.CanHitTargetFrom(base.Position, localTargetInfo))
		{
			return localTargetInfo;
		}
		return base.TryFindNewTarget();
	}
}
