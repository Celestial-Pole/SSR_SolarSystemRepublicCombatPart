using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

public abstract class AttachmentBase : IAttachable, IExposable
{
	private AttachmentDef _def;

	private int uniqueID = -1;

	private float healthInt = -1f;

	private bool isBroken;

	[Unsaved(false)]
	public bool useDebugValues;

	[Unsaved(false)]
	public Vector3 debugOffset = Vector3.zero;

	[Unsaved(false)]
	public Vector3 debugScale = Vector3.one;

	[Unsaved(false)]
	public Vector3 debugRotation = Vector3.zero;

	[Unsaved(false)]
	public ApparelLayerDef debugApparelLayer;

	public AttachmentDef def
	{
		get
		{
			return _def;
		}
		internal set
		{
			_def = value;
		}
	}

	public ISlotContainer Parent { get; private set; }

	public string SlotId { get; private set; }

	public Thing RootHost => Parent?.RootHost;

	public Pawn Holder => Parent?.Holder;

	public string Label => def.label;

	public string LabelCap => def.LabelCap;

	public int UniqueID
	{
		get
		{
			if (uniqueID == -1)
			{
				uniqueID = Find.UniqueIDsManager.GetNextThingID();
			}
			return uniqueID;
		}
	}

	public virtual float MaxHP => def.apparelProperties?.maxHP ?? (-1f);

	public float HP
	{
		get
		{
			if (!(healthInt < 0f))
			{
				return healthInt;
			}
			return MaxHP;
		}
		set
		{
			float num = Mathf.Clamp(value, 0f, MaxHP);
			if (healthInt != num)
			{
				healthInt = num;
				bool flag = healthInt <= 0f;
				if (flag != isBroken)
				{
					isBroken = flag;
					OnStateChanged();
				}
				Parent?.Notify(this, AttachmentNotificationType.HealthChanged);
			}
		}
	}

	public bool IsBroken => isBroken;

	public virtual bool ShouldTick => def.tickWhileEquipped;

	public virtual void CollectSelfStats(List<StatModifier> outOffsets, List<StatModifier> outFactors, bool isFunctionallyDisabled = false)
	{
		bool flag = IsBroken || isFunctionallyDisabled;
		List<StatDef> list = def.failureLogic?.statsDisabledWhenBroken;
		if (def.statOffsets != null)
		{
			foreach (StatModifier statOffset in def.statOffsets)
			{
				if (!flag || list == null || !list.Contains(statOffset.stat))
				{
					outOffsets.Add(statOffset);
				}
			}
		}
		if (def.statFactors == null)
		{
			return;
		}
		foreach (StatModifier statFactor in def.statFactors)
		{
			if (!flag || list == null || !list.Contains(statFactor.stat))
			{
				outFactors.Add(statFactor);
			}
		}
	}

	public virtual void OnInstalled(ISlotContainer parent, string slotId)
	{
		Parent = parent;
		SlotId = slotId;
		if (ShouldTick)
		{
			Parent?.RegisterTickable(this);
		}
	}

	public virtual void OnUninstalled()
	{
		if (ShouldTick)
		{
			Parent?.DeregisterTickable(this);
		}
		Parent = null;
		SlotId = null;
	}

	public virtual void Notify_HostLoadedAndLinked(ISlotContainer parent, string slotId)
	{
		Parent = parent;
		SlotId = slotId;
		if (ShouldTick)
		{
			Parent?.RegisterTickable(this);
		}
	}

	protected virtual void OnStateChanged()
	{
		Parent?.Notify(this, AttachmentNotificationType.StateChanged);
	}

	public virtual void Tick()
	{
	}

	public virtual void ExposeData()
	{
		Scribe_Defs.Look(ref _def, "def");
		Scribe_Values.Look(ref uniqueID, "uniqueID", -1);
		Scribe_Values.Look(ref healthInt, "healthInt", -1f);
		Scribe_Values.Look(ref isBroken, "isBroken", defaultValue: false);
	}

	public virtual void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
	{
		absorbed = false;
	}

	public virtual void PostApplyDamage(DamageInfo dinfo, float damageDealt)
	{
	}
}
