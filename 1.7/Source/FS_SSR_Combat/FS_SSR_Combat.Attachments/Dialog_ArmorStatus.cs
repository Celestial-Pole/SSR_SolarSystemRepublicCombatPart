using System.Collections.Generic;
using System.Linq;
using System.Text;
using FS_SSR_Combat.CustomArmor;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

public class Dialog_ArmorStatus : Window
{
	private struct ArmorStatInfo
	{
		public IAttachable Contributor;

		public StatDef Stat;

		public float Value;
	}

	private SSR_CustomArmor armor;

	private Vector2 scrollPosition = Vector2.zero;

	private const float RowHeight = 24f;

	private const float IndentWidth = 20f;

	private static HashSet<StatDef> _armorStatDefsCache;

	public override Vector2 InitialSize => new Vector2(800f, 600f);

	public Dialog_ArmorStatus(SSR_CustomArmor armor)
	{
		this.armor = armor;
		draggable = true;
		forcePause = false;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = false;
		doCloseX = true;
		preventCameraMotion = false;
		resizeable = true;
	}

	public override void DoWindowContents(Rect inRect)
	{
		Text.Font = GameFont.Medium;
		Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40f), armor.LabelCap + " Status");
		Text.Font = GameFont.Small;
		float num = 40f;
		if (DrawResourcePools(new Rect(inRect.x, num, inRect.width, 60f)))
		{
			num += 60f;
		}
		Widgets.DrawLineHorizontal(inRect.x, num, inRect.width);
		num += 10f;
		Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, GetTotalTreeHeight(armor));
		Rect outRect = new Rect(inRect.x, num, inRect.width, inRect.height - num - 40f);
		Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
		DrawNode(armor, 0, new Rect(0f, 0f, viewRect.width, viewRect.height));
		Widgets.EndScrollView();
	}

	private float DrawNode(object node, int indentLevel, Rect currentRect)
	{
		float num = 0f;
		Rect rect = new Rect(currentRect.x + (float)indentLevel * 20f, currentRect.y, currentRect.width - (float)indentLevel * 20f, 24f);
		if (Mouse.IsOver(rect))
		{
			Widgets.DrawHighlight(rect);
		}
		string nodeLabel = GetNodeLabel(node);
		Widgets.Label(rect, nodeLabel);
		TooltipHandler.TipRegion(rect, () => GetNodeTooltip(node), node.GetHashCode());
		num += 24f;
		if (node is ISlotContainer slotContainer)
		{
			foreach (IAttachable allInstalledAttachment in slotContainer.AllInstalledAttachments)
			{
				Rect currentRect2 = new Rect(currentRect.x, currentRect.y + num, currentRect.width, currentRect.height - num);
				num += DrawNode(allInstalledAttachment, indentLevel + 1, currentRect2);
			}
		}
		return num;
	}

	private string GetNodeLabel(object node)
	{
		string text = "Unknown";
		string text2 = "";
		if (node is SSR_CustomArmor sSR_CustomArmor)
		{
			text = sSR_CustomArmor.LabelCap;
			text2 = $"({sSR_CustomArmor.HitPoints}/{sSR_CustomArmor.MaxHitPoints})";
		}
		else if (node is AttachmentBase attachmentBase)
		{
			text = attachmentBase.LabelCap;
			if (attachmentBase.MaxHP > 0f)
			{
				text2 = $"({attachmentBase.HP:F0}/{attachmentBase.MaxHP:F0})";
				if (attachmentBase.IsBroken)
				{
					text2 += " [BROKEN]";
				}
			}
		}
		return text + " " + text2;
	}

	private string GetNodeTooltip(object node)
	{
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		if (node is ISlotContainer container)
		{
			Dictionary<BodyPartDef, List<ArmorStatInfo>> dictionary = new Dictionary<BodyPartDef, List<ArmorStatInfo>>();
			CollectDetailedArmorStats(container, dictionary);
			if (dictionary.Any())
			{
				flag = true;
				stringBuilder.AppendLine("--- Armor Rating by Body Part ---");
				foreach (KeyValuePair<BodyPartDef, List<ArmorStatInfo>> item in dictionary.OrderBy((KeyValuePair<BodyPartDef, List<ArmorStatInfo>> p) => p.Key.label))
				{
					BodyPartDef key = item.Key;
					List<ArmorStatInfo> value = item.Value;
					stringBuilder.AppendLine($"\n[ {key.LabelCap} ]");
					IEnumerable<IGrouping<StatDef, ArmorStatInfo>> enumerable = from s in value
						group s by s.Stat;
					foreach (IGrouping<StatDef, ArmorStatInfo> item2 in enumerable)
					{
						float a = item2.Sum((ArmorStatInfo s) => s.Value);
						float num = Mathf.Min(a, 2f);
						stringBuilder.AppendLine($"  {item2.Key.LabelCap}: {num:P0}");
						if (item2.Count() <= 1)
						{
							continue;
						}
						foreach (ArmorStatInfo item3 in item2)
						{
							stringBuilder.AppendLine($"    - {item3.Contributor.def.LabelCap}: {item3.Value:P0}");
						}
					}
				}
			}
		}
		if (node is ISlotContainer { StatCache: var statCache })
		{
			statCache.EnsureValid();
			List<KeyValuePair<StatDef, (float, float)>> list = statCache._cachedStats.Where((KeyValuePair<StatDef, (float totalOffset, float totalFactor)> kv) => !IsArmorStat(kv.Key) && (kv.Value.totalOffset != 0f || kv.Value.totalFactor != 1f)).ToList();
			if (list.Any())
			{
				if (flag)
				{
					stringBuilder.AppendLine();
				}
				flag = true;
				stringBuilder.AppendLine("--- Global Stat Modifiers (Total) ---");
				foreach (KeyValuePair<StatDef, (float, float)> item4 in list)
				{
					string text = ((item4.Value.Item1 != 0f) ? $"Offset: {item4.Value.Item1:F2}" : "");
					string text2 = ((item4.Value.Item2 != 1f) ? $"Factor: {item4.Value.Item2:P1}" : "");
					stringBuilder.AppendLine(string.Format("{0}: {1}", item4.Key.LabelCap, (text + " " + text2).Trim()));
				}
			}
		}
		if (node is IAttachable attachable && !attachable.def.parentStatModifiers.NullOrEmpty())
		{
			if (flag)
			{
				stringBuilder.AppendLine();
			}
			flag = true;
			stringBuilder.AppendLine("--- Parent Stat Modifiers ---");
			foreach (AttachmentStatModifier parentStatModifier in attachable.def.parentStatModifiers)
			{
				string arg = ((parentStatModifier.resource != null) ? $" ({parentStatModifier.resource.LabelCap})" : "");
				stringBuilder.AppendLine($"{parentStatModifier.stat.LabelCap}{arg}: +{parentStatModifier.value}");
			}
		}
		if (!flag)
		{
			return "No special stats provided.";
		}
		return stringBuilder.ToString().TrimEnd();
	}

	private void CollectDetailedArmorStats(ISlotContainer container, Dictionary<BodyPartDef, List<ArmorStatInfo>> armorStatsByPart)
	{
		CollectRecursive(container, isParentFunctionallyDisabled: false);
		void CollectRecursive(object node, bool isParentFunctionallyDisabled)
		{
			IAttachable attachable = node as IAttachable;
			bool flag = attachable is AttachmentBase attachmentBase && attachmentBase.IsBroken;
			bool flag2 = isParentFunctionallyDisabled || flag;
			if (!flag2 && attachable != null)
			{
				IEnumerable<StatModifier> statOffsets = attachable.def.statOffsets;
				foreach (StatModifier item in statOffsets ?? Enumerable.Empty<StatModifier>())
				{
					if (IsArmorStat(item.stat))
					{
						AttachmentDef.ApparelProperties apparelProperties = attachable.def.apparelProperties;
						if (apparelProperties?.coverageRules != null)
						{
							foreach (CoverageRule coverageRule in apparelProperties.coverageRules)
							{
								if (coverageRule.part != null)
								{
									if (!armorStatsByPart.TryGetValue(coverageRule.part, out var value))
									{
										value = new List<ArmorStatInfo>();
										armorStatsByPart[coverageRule.part] = value;
									}
									value.Add(new ArmorStatInfo
									{
										Contributor = attachable,
										Stat = item.stat,
										Value = item.value
									});
								}
							}
						}
					}
				}
			}
			if (node is ISlotContainer slotContainer)
			{
				bool isParentFunctionallyDisabled2 = false;
				if (attachable != null)
				{
					isParentFunctionallyDisabled2 = flag2 && (attachable.def.failureLogic?.childAttachmentsFailWhenBroken ?? true);
				}
				foreach (IAttachable allInstalledAttachment in slotContainer.AllInstalledAttachments)
				{
					CollectRecursive(allInstalledAttachment, isParentFunctionallyDisabled2);
				}
			}
		}
	}

	private bool IsArmorStat(StatDef stat)
	{
		if (_armorStatDefsCache == null)
		{
			_armorStatDefsCache = new HashSet<StatDef>();
			foreach (DamageArmorCategoryDef allDef in DefDatabase<DamageArmorCategoryDef>.AllDefs)
			{
				if (allDef.armorRatingStat != null)
				{
					_armorStatDefsCache.Add(allDef.armorRatingStat);
				}
			}
		}
		return _armorStatDefsCache.Contains(stat);
	}

	private float GetTotalTreeHeight(ISlotContainer container, int level = 0)
	{
		float num = 24f;
		foreach (IAttachable allInstalledAttachment in container.AllInstalledAttachments)
		{
			num = ((!(allInstalledAttachment is ISlotContainer container2)) ? (num + 24f) : (num + GetTotalTreeHeight(container2, level + 1)));
		}
		return num;
	}

	private bool DrawResourcePools(Rect rect)
	{
		List<ResourcePool> list = (from def in DefDatabase<ResourceDef>.AllDefs
			select (!armor.TryGetResourcePool(def, out var pool)) ? null : pool into p
			where p != null && p.MaxValue > 0f
			orderby p.def.displayOrder
			select p).ToList();
		if (!list.Any())
		{
			return false;
		}
		Text.Font = GameFont.Small;
		float num = (rect.width - (float)(list.Count - 1) * 10f) / (float)list.Count;
		for (int num2 = 0; num2 < list.Count; num2++)
		{
			ResourcePool resourcePool = list[num2];
			Rect rect2 = new Rect(rect.x + (float)num2 * (num + 10f), rect.y + 20f, num, 30f);
			Widgets.FillableBar(rect2, resourcePool.Percent, SolidColorMaterials.NewSolidColorTexture(resourcePool.def.barColor));
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect2, $"{resourcePool.CurrentValue:F0} / {resourcePool.MaxValue:F0}");
			Text.Anchor = TextAnchor.UpperLeft;
			Rect rect3 = new Rect(rect2.x, rect.y, rect2.width, 20f);
			Text.Anchor = TextAnchor.LowerCenter;
			Widgets.Label(rect3, resourcePool.def.LabelCap);
			Text.Anchor = TextAnchor.UpperLeft;
		}
		return true;
	}
}
