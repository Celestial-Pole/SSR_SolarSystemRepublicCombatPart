using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FS_SSR_Combat;

public class ConfigDef : Def
{
	private static ConfigDef instance;

	public Dictionary<StatDef, float> ITF_ArmorOptions = new Dictionary<StatDef, float>();

	public float ITF_Health = 1f;

	public float ITF_DPS = 1f;

	public float ITF_DPH = 1f;

	public float ITF_Range = 1f;

	public static ConfigDef Config => instance;

	public ConfigDef()
	{
		instance = this;
	}
}
