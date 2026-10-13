using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

public class ResourcePool : IExposable
{
	public ResourceDef def;

	private float currentValue;

	private float maxValue;

	private bool isDirty = true;

	public float MaxValue => maxValue;

	public float CurrentValue
	{
		get
		{
			return currentValue;
		}
		set
		{
			currentValue = Mathf.Clamp(value, 0f, maxValue);
		}
	}

	public float Percent
	{
		get
		{
			if (!(maxValue > 0f))
			{
				return 0f;
			}
			return currentValue / maxValue;
		}
	}

	public bool IsDirty => isDirty;

	public ResourcePool()
	{
	}

	public ResourcePool(ResourceDef def)
	{
		this.def = def;
	}

	public void MarkDirty()
	{
		isDirty = true;
	}

	public void RecalculateMaxValue(float newMax)
	{
		maxValue = ((newMax > 0f) ? newMax : 0f);
		CurrentValue = currentValue;
		isDirty = false;
	}

	public void ExposeData()
	{
		Scribe_Defs.Look(ref def, "def");
		Scribe_Values.Look(ref currentValue, "currentValue", 0f);
	}
}
