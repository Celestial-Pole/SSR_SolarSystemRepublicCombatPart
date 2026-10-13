using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FS_SSR.Defense;

public class DefExt_SSRTurretInfo : DefModExtension
{
	public float yawRotationSpeed = 5f;

	public float pitchRotationSpeed = 5f;

	public string shootingOrigine;

	public string rootTransform;

	public string yawPath;

	public string pitchPath;

	public string shootingAnimate;

	public Vector2 yawRotationRange = new Vector2(0f, 0f);

	public Vector2 pitchRotationRange = new Vector2(270f, 90f);

	public Vector3 yawRotationAixe = Vector3.up;

	public Vector3 pitchRotationAixe = Vector3.right;

	public Vector3 shootingOrigineForward = Vector3.forward;

	public SoundDef shootingSoundDef;

	public List<ThingDef> subTurrets = new List<ThingDef>();

	private bool shootingOrigineInited;

	private bool rootTransformInited;

	private bool yawPathInited;

	private bool pitchPathInited;

	private bool shootingAnimateInited;

	private bool yawRotationRangeInited;

	private bool pitchRotationRangeInited;

	private bool yawRotationAixeInited;

	private bool pitchRotationAixeInited;

	public Vector2 YawRotationRange
	{
		get
		{
			if (!yawRotationRangeInited)
			{
				yawRotationRange.x %= 360f;
				yawRotationRange.y %= 360f;
				if (yawRotationRange.x < 0f)
				{
					yawRotationRange.x += 360f;
				}
				else if (yawRotationRange.x > 360f)
				{
					yawRotationRange.x -= 360f;
				}
				if (yawRotationRange.y < 0f)
				{
					yawRotationRange.y += 360f;
				}
				else if (yawRotationRange.y > 360f)
				{
					yawRotationRange.y -= 360f;
				}
			}
			return yawRotationRange;
		}
	}

	public Vector2 PitchRotationRange
	{
		get
		{
			if (!pitchRotationRangeInited)
			{
				pitchRotationRange.x %= 360f;
				pitchRotationRange.y %= 360f;
				if (pitchRotationRange.x < 0f)
				{
					pitchRotationRange.x += 360f;
				}
				else if (pitchRotationRange.x > 360f)
				{
					pitchRotationRange.x -= 360f;
				}
				if (pitchRotationRange.y < 0f)
				{
					pitchRotationRange.y += 360f;
				}
				else if (pitchRotationRange.y > 360f)
				{
					pitchRotationRange.y -= 360f;
				}
			}
			return pitchRotationRange;
		}
	}

	public Vector3 YawRotationAixe
	{
		get
		{
			if (!yawRotationAixeInited)
			{
				yawRotationAixe.Normalize();
			}
			return yawRotationAixe;
		}
	}

	public Vector3 PitchRotationAixe
	{
		get
		{
			if (!pitchRotationAixeInited)
			{
				pitchRotationAixe.Normalize();
			}
			return pitchRotationAixe;
		}
	}

	public string ShootingOrigine
	{
		get
		{
			if (!shootingOrigineInited)
			{
				if (shootingOrigine != null)
				{
					shootingOrigine = shootingOrigine.Replace(" ", "");
					shootingOrigine = shootingOrigine.Replace("\n", "");
					shootingOrigine = shootingOrigine.Replace("\r", "");
					shootingOrigine = shootingOrigine.Replace("\t", "");
				}
				shootingOrigineInited = true;
			}
			return shootingOrigine;
		}
	}

	public string RootTransform
	{
		get
		{
			if (!rootTransformInited)
			{
				if (rootTransform != null)
				{
					rootTransform = rootTransform.Replace(" ", "");
					rootTransform = rootTransform.Replace("\n", "");
					rootTransform = rootTransform.Replace("\r", "");
					rootTransform = rootTransform.Replace("\t", "");
				}
				rootTransformInited = true;
			}
			return rootTransform;
		}
	}

	public string YawPath
	{
		get
		{
			if (!yawPathInited)
			{
				if (yawPath != null)
				{
					yawPath = yawPath.Replace(" ", "");
					yawPath = yawPath.Replace("\n", "");
					yawPath = yawPath.Replace("\r", "");
					yawPath = yawPath.Replace("\t", "");
				}
				yawPathInited = true;
			}
			return yawPath;
		}
	}

	public string PitchPath
	{
		get
		{
			if (!pitchPathInited)
			{
				if (pitchPath != null)
				{
					pitchPath = pitchPath.Replace(" ", "");
					pitchPath = pitchPath.Replace("\n", "");
					pitchPath = pitchPath.Replace("\r", "");
					pitchPath = pitchPath.Replace("\t", "");
				}
				pitchPathInited = true;
			}
			return pitchPath;
		}
	}

	public string ShootingAnimate
	{
		get
		{
			if (!shootingAnimateInited)
			{
				if (shootingAnimate != null)
				{
					shootingAnimate = shootingAnimate.Replace(" ", "");
					shootingAnimate = shootingAnimate.Replace("\n", "");
					shootingAnimate = shootingAnimate.Replace("\r", "");
					shootingAnimate = shootingAnimate.Replace("\t", "");
				}
				shootingAnimateInited = true;
			}
			return shootingAnimate;
		}
	}
}
