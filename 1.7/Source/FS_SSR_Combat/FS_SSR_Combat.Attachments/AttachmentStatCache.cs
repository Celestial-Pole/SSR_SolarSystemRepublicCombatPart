using System.Collections.Generic;
using RimWorld;

namespace FS_SSR_Combat.Attachments;

public class AttachmentStatCache
{
	private readonly ISlotContainer owner;

	internal Dictionary<StatDef, (float totalOffset, float totalFactor)> _cachedStats;

	private bool isDirty = true;

	public AttachmentStatCache(ISlotContainer owner)
	{
		this.owner = owner;
		_cachedStats = new Dictionary<StatDef, (float, float)>();
	}

	public void MarkDirty()
	{
		isDirty = true;
	}

	public void EnsureValid()
	{
		if (isDirty)
		{
			Recalculate();
		}
	}

	public float GetStatOffset(StatDef stat)
	{
		EnsureValid();
		if (!_cachedStats.TryGetValue(stat, out (float, float) value))
		{
			return 0f;
		}
		return value.Item1;
	}

	public float GetStatFactor(StatDef stat)
	{
		EnsureValid();
		if (!_cachedStats.TryGetValue(stat, out (float, float) value))
		{
			return 1f;
		}
		return value.Item2;
	}

	private void Recalculate()
	{
		_cachedStats.Clear();
		var (list, list2) = CollectStatsRecursive(owner, isParentFunctionallyDisabled: false);
		foreach (StatModifier item in list)
		{
			if (!_cachedStats.TryGetValue(item.stat, out (float, float) value))
			{
				value = (0f, 1f);
			}
			value.Item1 += item.value;
			_cachedStats[item.stat] = value;
		}
		foreach (StatModifier item2 in list2)
		{
			if (!_cachedStats.TryGetValue(item2.stat, out (float, float) value2))
			{
				value2 = (0f, 1f);
			}
			value2.Item2 *= item2.value;
			_cachedStats[item2.stat] = value2;
		}
		isDirty = false;
	}

	private (List<StatModifier> offsets, List<StatModifier> factors) CollectStatsRecursive(object node, bool isParentFunctionallyDisabled)
	{
		List<StatModifier> list = new List<StatModifier>();
		List<StatModifier> list2 = new List<StatModifier>();
		IAttachable attachable = node as IAttachable;
		bool flag = attachable is AttachmentBase attachmentBase && attachmentBase.IsBroken;
		bool flag2 = isParentFunctionallyDisabled || flag;
		attachable?.CollectSelfStats(list, list2, flag2);
		if (node is ISlotContainer slotContainer)
		{
			bool isParentFunctionallyDisabled2 = false;
			if (attachable != null)
			{
				isParentFunctionallyDisabled2 = flag2 && (attachable.def.failureLogic?.childAttachmentsFailWhenBroken ?? true);
			}
			foreach (IAttachable allInstalledAttachment in slotContainer.AllInstalledAttachments)
			{
				var (collection, collection2) = CollectStatsRecursive(allInstalledAttachment, isParentFunctionallyDisabled2);
				list.AddRange(collection);
				list2.AddRange(collection2);
			}
		}
		return (offsets: list, factors: list2);
	}
}
