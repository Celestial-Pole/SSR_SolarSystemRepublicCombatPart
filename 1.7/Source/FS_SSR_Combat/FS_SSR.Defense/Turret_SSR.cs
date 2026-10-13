using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FS_SSR.Defense;

public class Turret_SSR : Building_TurretGun
{
	private delegate void _DrawAt(Thing _this, Vector3 drawLoc, bool flip = false);

	private bool currentAngleDiffCacheDirty = true;

	private bool currentAngleDiffSuccessCache;

	private float currentYawAngle;

	private float currentPitchAngle;

	private float currentYawAngleDiffCache;

	private float currentPitchAngleDiffCache;

	private Animator shootingAnimatorCache;

	private Transform shootingOrigineCache;

	private Transform rootTransformCache;

	private Transform yawTransformCache;

	private Transform pitchTransformCache;

	private Sustainer currentSustainer;

	private List<LocalTargetInfo> subTurrets = new List<LocalTargetInfo>();

	protected TC_PrefabDrawer prefabDrawerCache;

	protected DefExt_SSRTurretInfo propsCache;

	private static readonly int warmupHash = Animator.StringToHash("warmup");

	private static readonly int shootingHash = Animator.StringToHash("shooting");

	private static readonly int coldDownHash = Animator.StringToHash("coldDown");

	private static _DrawAt ThingWithConmp_DrawAt;

	public virtual DefExt_SSRTurretInfo Props
	{
		get
		{
			if (propsCache == null)
			{
				propsCache = def.GetModExtension<DefExt_SSRTurretInfo>();
			}
			if (propsCache == null)
			{
				propsCache = new DefExt_SSRTurretInfo();
				def.modExtensions.Add(propsCache);
			}
			return propsCache;
		}
	}

	public virtual TC_PrefabDrawer PrefabDrawer
	{
		get
		{
			if (prefabDrawerCache == null)
			{
				prefabDrawerCache = ((ThingWithComps)this).GetComp<TC_PrefabDrawer>();
			}
			return prefabDrawerCache;
		}
	}

	public override Vector3 DrawPos
	{
		get
		{
			StackFrame frame = new StackTrace().GetFrame(1);
			if (typeof(Verb).IsAssignableFrom(frame.GetMethod().DeclaringType))
			{
				Transform shootingOrigine = ShootingOrigine;
				if ((bool)shootingOrigine)
				{
					return shootingOrigine.position;
				}
			}
			return base.DrawPos;
		}
	}

	public float CurrentYawAngle
	{
		get
		{
			return currentYawAngle;
		}
		set
		{
			currentYawAngle = value % 360f;
			if (currentYawAngle < 0f)
			{
				currentYawAngle += 360f;
			}
			else if (currentYawAngle > 360f)
			{
				currentYawAngle -= 360f;
			}
			Vector2 yawRotationRange = Props.YawRotationRange;
			if (yawRotationRange.x == yawRotationRange.y)
			{
				return;
			}
			if (yawRotationRange.x > yawRotationRange.y)
			{
				if (!(currentYawAngle > yawRotationRange.x) && !(currentYawAngle < yawRotationRange.y))
				{
					if (yawRotationRange.x - currentYawAngle < currentYawAngle - yawRotationRange.y)
					{
						currentYawAngle = yawRotationRange.x;
					}
					else
					{
						currentYawAngle = yawRotationRange.y;
					}
				}
			}
			else
			{
				currentYawAngle = Mathf.Clamp(currentYawAngle, yawRotationRange.x, yawRotationRange.y);
			}
		}
	}

	public float CurrentPitchAngle
	{
		get
		{
			return currentPitchAngle;
		}
		set
		{
			currentPitchAngle = value % 360f;
			if (currentPitchAngle < 0f)
			{
				currentPitchAngle += 360f;
			}
			else if (currentPitchAngle > 360f)
			{
				currentPitchAngle -= 360f;
			}
			Vector2 pitchRotationRange = Props.PitchRotationRange;
			if (pitchRotationRange.x == pitchRotationRange.y)
			{
				return;
			}
			if (pitchRotationRange.x > pitchRotationRange.y)
			{
				if (!(currentPitchAngle > pitchRotationRange.x) && !(currentPitchAngle < pitchRotationRange.y))
				{
					if (pitchRotationRange.x - currentPitchAngle < currentPitchAngle - pitchRotationRange.y)
					{
						currentPitchAngle = pitchRotationRange.x;
					}
					else
					{
						currentPitchAngle = pitchRotationRange.y;
					}
				}
			}
			else
			{
				currentPitchAngle = Mathf.Clamp(currentPitchAngle, pitchRotationRange.x, pitchRotationRange.y);
			}
		}
	}

	public virtual Vector3 TargetRelativePostion
	{
		get
		{
			if (CurrentTarget != LocalTargetInfo.Invalid)
			{
				return ((CurrentTarget.HasThing ? CurrentTarget.Thing.DrawPos : CurrentTarget.CenterVector3) - base.DrawPos).Yto0();
			}
			return 1024f * (Quaternion.AngleAxis(top.CurRotation, Vector3.up) * Vector3.forward);
		}
	}

	public (float yaw, float pitch, bool success) CurrentAngleDiff
	{
		get
		{
			if (!currentAngleDiffCacheDirty)
			{
				return (yaw: currentYawAngleDiffCache, pitch: currentPitchAngleDiffCache, success: currentAngleDiffSuccessCache);
			}
			Transform yawTransform = YawTransform;
			Transform pitchTransform = PitchTransform;
			Transform rootTransform = RootTransform;
			Transform shootingOrigine = ShootingOrigine;
			if (!rootTransform || !shootingOrigine || !shootingOrigine.IsChildOf(rootTransform))
			{
				return (yaw: 0f, pitch: 0f, success: true);
			}
			Vector3 yawRotationAixe = Props.YawRotationAixe;
			Vector3 pitchRotationAixe = Props.PitchRotationAixe;
			Vector2 yawRotationRange = Props.YawRotationRange;
			Vector2 pitchRotationRange = Props.PitchRotationRange;
			Vector3 vector = rootTransform.TransformPoint(TargetRelativePostion);
			float num = CurrentYawAngle;
			float num2 = CurrentPitchAngle;
			currentAngleDiffSuccessCache = false;
			for (int i = 0; i < 16; i++)
			{
				Transform transform = shootingOrigine;
				Vector3 vector2 = shootingOrigine.TransformVector(Props.shootingOrigineForward);
				Vector3 position = shootingOrigine.position;
				if (Vector3.Dot((vector - position).normalized, vector2.normalized) > 0.9999f)
				{
					currentAngleDiffSuccessCache = true;
					break;
				}
				while ((bool)transform && transform.IsChildOf(rootTransform))
				{
					if (transform == yawTransform)
					{
						Vector3 vector3 = transform.InverseTransformPoint(position);
						vector3 -= Vector3.Dot(vector3, yawRotationAixe) * yawRotationAixe;
						Vector3 vector4 = transform.InverseTransformPoint(vector);
						vector4 -= Vector3.Dot(vector4, yawRotationAixe) * yawRotationAixe;
						if (vector3.sqrMagnitude > 0.0001f && vector4.sqrMagnitude > 0.0001f)
						{
							Vector3 vector5 = transform.InverseTransformVector(vector2);
							vector5 = (vector5 - Vector3.Dot(vector5, yawRotationAixe) * yawRotationAixe).normalized;
							Vector2 vector6 = ((yawRotationRange.x == yawRotationRange.y) ? new Vector2(-180f, 180f) : ((!(yawRotationRange.x > yawRotationRange.y)) ? new Vector2(yawRotationRange.x - num, yawRotationRange.y - num) : ((!(num >= yawRotationRange.x)) ? new Vector2(yawRotationRange.x - num - 360f, yawRotationRange.y - num) : new Vector2(yawRotationRange.x - num, yawRotationRange.y - num + 360f))));
							vector3 -= Vector3.Dot(vector3, vector5) * vector5;
							vector3 += Mathf.Sqrt(vector4.sqrMagnitude - vector3.sqrMagnitude) * vector5;
							float value = Vector3.SignedAngle(vector3, vector4, yawRotationAixe);
							float num3 = Mathf.Clamp(value, vector6.x, vector6.y);
							num += num3;
							transform.localRotation = Quaternion.AngleAxis(num, yawRotationAixe);
							vector2 = shootingOrigine.TransformVector(Props.shootingOrigineForward);
							position = shootingOrigine.position;
						}
					}
					if (transform == pitchTransform)
					{
						Vector3 vector7 = transform.InverseTransformPoint(position);
						vector7 -= Vector3.Dot(vector7, pitchRotationAixe) * pitchRotationAixe;
						Vector3 vector8 = transform.InverseTransformPoint(vector);
						vector8 -= Vector3.Dot(vector8, pitchRotationAixe) * pitchRotationAixe;
						if (vector7.sqrMagnitude > 0.0001f && vector8.sqrMagnitude > 0.0001f)
						{
							Vector3 vector9 = transform.InverseTransformVector(vector2);
							vector9 = (vector9 - Vector3.Dot(vector9, pitchRotationAixe) * pitchRotationAixe).normalized;
							Vector2 vector10 = ((pitchRotationRange.x == pitchRotationRange.y) ? new Vector2(-180f, 180f) : ((!(pitchRotationRange.x > pitchRotationRange.y)) ? new Vector2(pitchRotationRange.x - num2, pitchRotationRange.y - num2) : ((!(num2 >= pitchRotationRange.x)) ? new Vector2(pitchRotationRange.x - num2 - 360f, pitchRotationRange.y - num2) : new Vector2(pitchRotationRange.x - num2, pitchRotationRange.y - num2 + 360f))));
							vector7 -= Vector3.Dot(vector7, vector9) * vector9;
							vector7 += Mathf.Sqrt(vector8.sqrMagnitude - vector7.sqrMagnitude) * vector9;
							float value2 = Vector3.SignedAngle(vector7, vector8, pitchRotationAixe);
							float num4 = Mathf.Clamp(value2, vector10.x, vector10.y);
							num2 += num4;
							transform.localRotation = Quaternion.AngleAxis(num2, pitchRotationAixe);
							vector2 = shootingOrigine.TransformVector(Props.shootingOrigineForward);
							position = shootingOrigine.position;
						}
					}
					transform = transform.parent;
				}
			}
			if ((bool)yawTransform)
			{
				yawTransform.localRotation = Quaternion.AngleAxis(CurrentYawAngle, yawRotationAixe);
			}
			if ((bool)pitchTransform)
			{
				pitchTransform.localRotation = Quaternion.AngleAxis(CurrentPitchAngle, pitchRotationAixe);
			}
			currentYawAngleDiffCache = num - CurrentYawAngle;
			currentPitchAngleDiffCache = num2 - CurrentPitchAngle;
			currentAngleDiffCacheDirty = false;
			return (yaw: currentYawAngleDiffCache, pitch: currentPitchAngleDiffCache, success: currentAngleDiffSuccessCache);
		}
	}

	public Animator ShootingAnimator
	{
		get
		{
			if (!shootingAnimatorCache)
			{
				TC_PrefabDrawer prefabDrawer = PrefabDrawer;
				if (prefabDrawer != null)
				{
					string[] array = Props.ShootingAnimate?.Split('/');
					if (array != null)
					{
						Transform transform = ((Component)(object)prefabDrawer.CurrentUnityObject).transform;
						string[] array2 = array;
						foreach (string n in array2)
						{
							if ((bool)transform)
							{
								transform = transform.Find(n);
								continue;
							}
							return shootingAnimatorCache;
						}
						shootingAnimatorCache = transform.GetComponent<Animator>();
					}
				}
			}
			return shootingAnimatorCache;
		}
	}

	public Transform ShootingOrigine
	{
		get
		{
			if (!shootingOrigineCache)
			{
				TC_PrefabDrawer prefabDrawer = PrefabDrawer;
				if (prefabDrawer != null)
				{
					string[] array = Props.ShootingOrigine?.Split('/');
					if (array != null)
					{
						Transform transform = ((Component)(object)prefabDrawer.CurrentUnityObject).transform;
						string[] array2 = array;
						foreach (string n in array2)
						{
							if ((bool)transform)
							{
								transform = transform.Find(n);
								continue;
							}
							return null;
						}
						shootingOrigineCache = transform;
					}
				}
			}
			return shootingOrigineCache;
		}
	}

	public Transform RootTransform
	{
		get
		{
			if (!rootTransformCache)
			{
				TC_PrefabDrawer prefabDrawer = PrefabDrawer;
				if (prefabDrawer != null)
				{
					string[] array = Props.RootTransform?.Split('/');
					if (array != null)
					{
						Transform transform = ((Component)(object)prefabDrawer.CurrentUnityObject).transform;
						string[] array2 = array;
						foreach (string n in array2)
						{
							if ((bool)transform)
							{
								transform = transform.Find(n);
								continue;
							}
							return rootTransformCache;
						}
						rootTransformCache = transform;
					}
				}
			}
			return rootTransformCache;
		}
	}

	public Transform YawTransform
	{
		get
		{
			if (!yawTransformCache)
			{
				TC_PrefabDrawer prefabDrawer = PrefabDrawer;
				if (prefabDrawer != null)
				{
					string[] array = Props.YawPath?.Split('/');
					if (array != null)
					{
						Transform transform = ((Component)(object)prefabDrawer.CurrentUnityObject).transform;
						string[] array2 = array;
						foreach (string n in array2)
						{
							if ((bool)transform)
							{
								transform = transform.Find(n);
								continue;
							}
							return null;
						}
						yawTransformCache = transform;
					}
				}
			}
			return yawTransformCache;
		}
	}

	public Transform PitchTransform
	{
		get
		{
			if (!pitchTransformCache)
			{
				TC_PrefabDrawer prefabDrawer = PrefabDrawer;
				if (prefabDrawer != null)
				{
					string[] array = Props.PitchPath?.Split('/');
					if (array != null)
					{
						Transform transform = ((Component)(object)prefabDrawer.CurrentUnityObject).transform;
						string[] array2 = array;
						foreach (string n in array2)
						{
							if ((bool)transform)
							{
								transform = transform.Find(n);
								continue;
							}
							return null;
						}
						pitchTransformCache = transform;
					}
				}
			}
			return pitchTransformCache;
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Values.Look(ref currentYawAngle, "currentYawAngle", 0f);
		Scribe_Values.Look(ref currentPitchAngle, "currentPitchAngle", 0f);
		Scribe_Collections.Look(ref subTurrets, "subTurrets", LookMode.LocalTargetInfo);
		if (Scribe.mode != LoadSaveMode.ResolvingCrossRefs)
		{
			return;
		}
		foreach (LocalTargetInfo subTurret in subTurrets)
		{
			((SubTurret_SSR)subTurret.Thing).parent = this;
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);
		if (respawningAfterLoad || Props.subTurrets.NullOrEmpty())
		{
			return;
		}
		foreach (ThingDef subTurret in Props.subTurrets)
		{
			if (typeof(SubTurret_SSR).IsAssignableFrom(subTurret.thingClass))
			{
				SubTurret_SSR subTurret_SSR = (SubTurret_SSR)ThingMaker.MakeThing(subTurret);
				subTurret_SSR.parent = this;
				subTurrets.Add(subTurret_SSR);
				subTurret_SSR.Position = base.Position;
				subTurret_SSR.SpawnSetup(map, respawningAfterLoad);
			}
		}
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		base.DeSpawn(mode);
		foreach (LocalTargetInfo subTurret in subTurrets)
		{
			subTurret.Thing.Destroy();
		}
		subTurrets.Clear();
	}

	protected override void BeginBurst()
	{
		(float yaw, float pitch, bool success) currentAngleDiff = CurrentAngleDiff;
		var (f, f2, _) = currentAngleDiff;
		if (currentAngleDiff.success && Mathf.Abs(f) < 0.0001f && Mathf.Abs(f2) < 0.0001f)
		{
			base.BeginBurst();
		}
		else
		{
			burstWarmupTicksLeft = 1;
		}
	}

	protected override void Tick()
	{
		Animator shootingAnimator = ShootingAnimator;
		SoundDef shootingSoundDef = Props.shootingSoundDef;
		var (num, num2, flag) = CurrentAngleDiff;
		base.Tick();
		if (currentSustainer != null)
		{
			if (currentSustainer.Ended)
			{
				currentSustainer = null;
			}
			else
			{
				currentSustainer.Maintain();
			}
		}
		if (AttackVerb.state == VerbState.Bursting)
		{
			if ((bool)shootingAnimator)
			{
				shootingAnimator.SetBool(warmupHash, value: true);
				shootingAnimator.SetBool(shootingHash, value: true);
				shootingAnimator.SetBool(coldDownHash, value: false);
			}
			if (shootingSoundDef != null && currentSustainer == null)
			{
				currentSustainer = shootingSoundDef.TrySpawnSustainer(this);
			}
		}
		else
		{
			if (currentSustainer != null)
			{
				currentSustainer.End();
			}
			if (burstWarmupTicksLeft > 0 && CurrentTarget.IsValid)
			{
				if ((bool)shootingAnimator)
				{
					shootingAnimator.SetBool(coldDownHash, value: true);
					shootingAnimator.SetBool(warmupHash, value: true);
					shootingAnimator.SetBool(shootingHash, value: false);
				}
			}
			else if ((bool)shootingAnimator)
			{
				shootingAnimator.SetBool(shootingHash, value: true);
				shootingAnimator.SetBool(coldDownHash, value: true);
				shootingAnimator.SetBool(warmupHash, value: false);
			}
		}
		currentAngleDiffCacheDirty = true;
		if (flag)
		{
			float num3 = Mathf.Sign(num);
			if (60f * num * num3 < Mathf.Abs(Props.yawRotationSpeed))
			{
				CurrentYawAngle += num;
			}
			else
			{
				CurrentYawAngle += Mathf.Abs(Props.yawRotationSpeed) * num3 / 60f;
			}
			num3 = Mathf.Sign(num2);
			if (60f * num2 * num3 < Mathf.Abs(Props.pitchRotationSpeed))
			{
				CurrentPitchAngle += num2;
			}
			else
			{
				CurrentPitchAngle += Mathf.Abs(Props.pitchRotationSpeed) * num3 / 60f;
			}
		}
	}

	protected override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		Transform yawTransform = YawTransform;
		Transform pitchTransform = PitchTransform;
		Vector3 yawRotationAixe = Props.yawRotationAixe;
		Vector3 pitchRotationAixe = Props.pitchRotationAixe;
		if ((bool)yawTransform)
		{
			yawTransform.localRotation = Quaternion.AngleAxis(CurrentYawAngle, yawRotationAixe);
		}
		if ((bool)pitchTransform)
		{
			if (pitchTransform != yawTransform)
			{
				pitchTransform.localRotation = Quaternion.AngleAxis(CurrentPitchAngle, pitchRotationAixe);
			}
			else
			{
				pitchTransform.localRotation *= Quaternion.AngleAxis(CurrentPitchAngle, pitchRotationAixe);
			}
		}
		if (ThingWithConmp_DrawAt == null)
		{
			Type[] array = new Type[3]
			{
				typeof(Thing),
				typeof(Vector3),
				typeof(bool)
			};
			MethodInfo method = typeof(ThingWithComps).GetMethod("DrawAt", BindingFlags.Instance | BindingFlags.NonPublic);
			DynamicMethod dynamicMethod = new DynamicMethod("ThingWithComps.DrawAt", typeof(void), array);
			ILGenerator iLGenerator = dynamicMethod.GetILGenerator();
			iLGenerator.Emit(OpCodes.Ldarg_0);
			iLGenerator.Emit(OpCodes.Ldarg_1);
			iLGenerator.Emit(OpCodes.Ldarg_2);
			iLGenerator.EmitCall(OpCodes.Call, method, array);
			iLGenerator.Emit(OpCodes.Ret);
			ThingWithConmp_DrawAt = (_DrawAt)dynamicMethod.CreateDelegate(typeof(_DrawAt));
		}
		ThingWithConmp_DrawAt(this, drawLoc, flip);
	}

	public override string GetInspectString()
	{
		string text = base.GetInspectString();
		if (DebugSettings.ShowDevGizmos)
		{
			text += "\n";
			text += $"Current Yaw         : {CurrentYawAngle}\n";
			text += $"Current Yaw Diff    : {CurrentAngleDiff.yaw}\n";
			text += $"Current Pitch       : {CurrentPitchAngle}\n";
			text += $"Current Pitch Diff  : {CurrentAngleDiff.pitch}";
		}
		return text;
	}
}
