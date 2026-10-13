using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FS_SSR_Combat.Attachments;

public class CompositeAttachment : AttachmentBase, ISlotContainer
{
	private Dictionary<string, List<IAttachable>> _installedAttachments;

	private AttachmentStatCache _statCache;

	private HashSet<IAttachable> _tickableChildren;

	private Dictionary<AttachmentStatDef, Dictionary<ResourceDef, float>> _internalResourceStatCache;

	private float _internalMaxHPBonusCache;

	private bool _internalStatsDirty = true;

	private float _cachedMaxHP = -1f;

	public AttachmentStatCache StatCache => _statCache;

	public IEnumerable<SlotPropertyDef> AllSlotProperties
	{
		get
		{
			IEnumerable<SlotPropertyDef> slots = base.def.slots;
			return slots ?? Enumerable.Empty<SlotPropertyDef>();
		}
	}

	public IEnumerable<IAttachable> AllInstalledAttachments => _installedAttachments.Values.SelectMany((List<IAttachable> list) => list);

	public override float MaxHP
	{
		get
		{
			if (_cachedMaxHP < 0f)
			{
				if (_internalStatsDirty)
				{
					RecalculateInternalStats();
				}
				_cachedMaxHP = base.MaxHP + _internalMaxHPBonusCache;
			}
			return _cachedMaxHP;
		}
	}

	public CompositeAttachment()
	{
		_installedAttachments = new Dictionary<string, List<IAttachable>>();
		_statCache = new AttachmentStatCache(this);
		_tickableChildren = new HashSet<IAttachable>();
	}

	public bool TryGetSlotProperty(string slotId, out SlotPropertyDef slotProperty)
	{
		slotProperty = AllSlotProperties.FirstOrDefault((SlotPropertyDef s) => s.defName == slotId);
		return slotProperty != null;
	}

	public IEnumerable<IAttachable> GetAttachmentsInSlot(string slotId)
	{
		if (_installedAttachments.TryGetValue(slotId, out var value))
		{
			return value;
		}
		return Enumerable.Empty<IAttachable>();
	}

	public bool CanInstall(string slotId, IAttachable item, out string reason)
	{
		reason = "";
		if (item == null || !TryGetSlotProperty(slotId, out var slotProperty))
		{
			reason = "内部错误：附件或槽位定义无效。";
			return false;
		}
		if (GetAttachmentsInSlot(slotId).Any())
		{
			reason = "槽位已被占用";
			return false;
		}
		IEnumerable<string> tags = slotProperty.tags;
		HashSet<string> other = new HashSet<string>(tags ?? Enumerable.Empty<string>());
		if (item.def.ExcludeParentSlotTagsSet.Overlaps(other))
		{
			reason = "附件与该槽位不兼容 (被排除)";
			return false;
		}
		if (!item.def.AllowedParentSlotTagsSet.IsSubsetOf(other))
		{
			reason = "附件类型不匹配";
			return false;
		}
		if (!slotProperty.mutexSlots.NullOrEmpty())
		{
			foreach (string mutexSlot in slotProperty.mutexSlots)
			{
				IAttachable attachable = GetAttachmentsInSlot(mutexSlot).FirstOrDefault();
				if (attachable != null)
				{
					if (TryGetSlotProperty(mutexSlot, out var slotProperty2))
					{
						reason = $"与 [{slotProperty2.LabelCap}] 槽中的 [{attachable.LabelCap}] 冲突";
					}
					return false;
				}
			}
		}
		foreach (KeyValuePair<string, List<IAttachable>> item2 in _installedAttachments.Where((KeyValuePair<string, List<IAttachable>> kvp) => kvp.Value.Any()))
		{
			if (TryGetSlotProperty(item2.Key, out var slotProperty3) && !slotProperty3.mutexSlots.NullOrEmpty() && slotProperty3.mutexSlots.Contains(slotProperty.defName))
			{
				reason = $"与已占用的 [{slotProperty3.LabelCap}] 槽互斥";
				return false;
			}
		}
		return true;
	}

	public bool CanInstall(string slotId, IAttachable item)
	{
		string reason;
		return CanInstall(slotId, item, out reason);
	}

	public bool TryInstall(string slotId, IAttachable item)
	{
		if (!CanInstall(slotId, item, out var _))
		{
			return false;
		}
		if (!_installedAttachments.TryGetValue(slotId, out var value))
		{
			value = new List<IAttachable>();
			_installedAttachments[slotId] = value;
		}
		value.Add(item);
		item.OnInstalled(this, slotId);
		Notify(item, AttachmentNotificationType.StatsChanged);
		return true;
	}

	public bool TryUninstall(IAttachable item)
	{
		if (item == null || item.SlotId == null)
		{
			return false;
		}
		if (TryGetSlotProperty(item.SlotId, out var slotProperty) && slotProperty.isEssential)
		{
			return false;
		}
		if (_installedAttachments.TryGetValue(item.SlotId, out var value) && value.Remove(item))
		{
			item.OnUninstalled();
			Notify(item, AttachmentNotificationType.StatsChanged);
			return true;
		}
		return false;
	}

	public void RegisterTickable(IAttachable tickable)
	{
		_tickableChildren.Add(tickable);
	}

	public void DeregisterTickable(IAttachable tickable)
	{
		_tickableChildren.Remove(tickable);
	}

	public void Notify(IAttachable source, AttachmentNotificationType type)
	{
		_statCache.MarkDirty();
		_internalStatsDirty = true;
		_cachedMaxHP = -1f;
		base.Parent?.Notify(this, type);
		if (type == AttachmentNotificationType.StatsChanged && base.HP > MaxHP)
		{
			base.HP = MaxHP;
		}
	}

	public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
	{
		base.PreApplyDamage(ref dinfo, out absorbed);
		if (base.IsBroken)
		{
			return;
		}
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			allInstalledAttachment.PreApplyDamage(ref dinfo, out var absorbed2);
			if (absorbed2)
			{
				absorbed = true;
				break;
			}
		}
	}

	public override void PostApplyDamage(DamageInfo dinfo, float damageDealt)
	{
		base.PostApplyDamage(dinfo, damageDealt);
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			allInstalledAttachment.PostApplyDamage(dinfo, damageDealt);
		}
	}

	public float GetInternalStatValue(AttachmentStatDef stat, ResourceDef resource)
	{
		if (_internalStatsDirty)
		{
			RecalculateInternalStats();
		}
		if (stat == AttachmentStatDefOf.ResourceCapacity && resource != null && _internalResourceStatCache.TryGetValue(stat, out var value) && value.TryGetValue(resource, out var value2))
		{
			return value2;
		}
		return 0f;
	}

	private void RecalculateInternalStats()
	{
		if (_internalResourceStatCache == null)
		{
			_internalResourceStatCache = new Dictionary<AttachmentStatDef, Dictionary<ResourceDef, float>>();
		}
		_internalResourceStatCache.Clear();
		_internalMaxHPBonusCache = 0f;
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			if (allInstalledAttachment.def.parentStatModifiers.NullOrEmpty() || allInstalledAttachment is AttachmentBase { IsBroken: not false })
			{
				continue;
			}
			foreach (AttachmentStatModifier parentStatModifier in allInstalledAttachment.def.parentStatModifiers)
			{
				if (parentStatModifier.stat == AttachmentStatDefOf.MaxHP)
				{
					_internalMaxHPBonusCache += parentStatModifier.value;
				}
				else if (parentStatModifier.stat == AttachmentStatDefOf.ResourceCapacity && parentStatModifier.resource != null)
				{
					if (!_internalResourceStatCache.TryGetValue(parentStatModifier.stat, out var value))
					{
						value = new Dictionary<ResourceDef, float>();
						_internalResourceStatCache[parentStatModifier.stat] = value;
					}
					if (!value.ContainsKey(parentStatModifier.resource))
					{
						value[parentStatModifier.resource] = 0f;
					}
					value[parentStatModifier.resource] += parentStatModifier.value;
				}
			}
		}
		_internalStatsDirty = false;
	}

	public override void OnInstalled(ISlotContainer parent, string slotId)
	{
		base.OnInstalled(parent, slotId);
	}

	public override void OnUninstalled()
	{
		foreach (IAttachable item in AllInstalledAttachments.ToList())
		{
			TryUninstall(item);
		}
		base.OnUninstalled();
	}

	public override void Notify_HostLoadedAndLinked(ISlotContainer parent, string slotId)
	{
		base.Notify_HostLoadedAndLinked(parent, slotId);
		foreach (KeyValuePair<string, List<IAttachable>> installedAttachment in _installedAttachments)
		{
			foreach (IAttachable item in installedAttachment.Value)
			{
				item.Notify_HostLoadedAndLinked(this, installedAttachment.Key);
			}
		}
	}

	public override void Tick()
	{
		base.Tick();
		foreach (IAttachable item in _tickableChildren.ToList())
		{
			if (item is AttachmentBase { IsBroken: false } attachmentBase)
			{
				attachmentBase.Tick();
			}
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();
		Scribe_Collections.Look(ref _installedAttachments, "installedAttachments", LookMode.Value, LookMode.Deep);
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			if (_statCache == null)
			{
				_statCache = new AttachmentStatCache(this);
			}
			if (_tickableChildren == null)
			{
				_tickableChildren = new HashSet<IAttachable>();
			}
			if (_installedAttachments == null)
			{
				_installedAttachments = new Dictionary<string, List<IAttachable>>();
			}
			_statCache.MarkDirty();
			_internalStatsDirty = true;
			_cachedMaxHP = -1f;
		}
	}
}
