using System;
using System.Collections.Generic;
using System.Linq;
using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.Attachments.ModExtension;
using FS_SSR_Combat.CustomWeapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FS_SSR_Combat.UI;

//在主线程预载武器调试面板使用的图标。
[StaticConstructorOnStartup]
public class Dialog_SSRWeaponDebugPanel : Window
{
	public readonly SSR_CustomWeapon weapon;

	private Vector2 attachmentsScrollPosition = Vector2.zero;

	private Vector2 statsScrollPosition = Vector2.zero;

	private HashSet<ISlotContainer> expandedNodes = new HashSet<ISlotContainer>();

	private const float RowHeight = 28f;

	public static readonly Texture2D PanelIcon;

	private Texture2D _canvasTextureCache;

	private Texture2D CanvasTexture
	{
		get
		{
			if (_canvasTextureCache == null)
			{
				SlottableDefExtension modExtension = weapon.def.GetModExtension<SlottableDefExtension>();
				if (modExtension != null)
				{
					string text = ((!modExtension.canvasTexPath.NullOrEmpty()) ? modExtension.canvasTexPath : weapon.def.graphicData?.texPath);
					if (!text.NullOrEmpty())
					{
						_canvasTextureCache = ContentFinder<Texture2D>.Get(text, reportFailure: false);
					}
				}
			}
			return _canvasTextureCache;
		}
	}

	public override Vector2 InitialSize => new Vector2(1000f, 700f);

	static Dialog_SSRWeaponDebugPanel()
	{
		//使用原版已提供的编辑图标。
		PanelIcon = TexButton.Rename;
	}

	public Dialog_SSRWeaponDebugPanel(SSR_CustomWeapon weapon)
	{
		this.weapon = weapon;
		expandedNodes.Add(weapon);
		doCloseX = true;
		closeOnClickedOutside = false;
		draggable = true;
		resizeable = true;
		absorbInputAroundWindow = false;
		forcePause = false;
	}

	public override void DoWindowContents(Rect inRect)
	{
		float num = 300f;
		float num2 = 250f;
		float width = inRect.width - num - num2 - 24f;
		Rect rect = new Rect(inRect.x, inRect.y, num, inRect.height);
		Rect rect2 = new Rect(rect.xMax + 12f, inRect.y, width, inRect.height);
		Rect rect3 = new Rect(rect2.xMax + 12f, inRect.y, num2, inRect.height);
		DrawAttachmentsPanel(rect);
		DrawPreviewPanel(rect2);
		DrawStatsPanel(rect3);
	}

	private bool IsSlotDisabled(ISlotContainer container, SlotPropertyDef slotToCheck, out string reason)
	{
		List<string> list = new List<string>();
		foreach (IAttachable allInstalledAttachment in container.AllInstalledAttachments)
		{
			if (container.TryGetSlotProperty(allInstalledAttachment.SlotId, out var slotProperty) && slotProperty.mutexSlots != null && slotProperty.mutexSlots.Contains(slotToCheck.defName))
			{
				string item = ((!slotProperty.label.NullOrEmpty()) ? slotProperty.label : slotProperty.defName);
				if (!list.Contains(item))
				{
					list.Add(item);
				}
			}
		}
		if (slotToCheck.mutexSlots != null)
		{
			foreach (string mutexSlot in slotToCheck.mutexSlots)
			{
				if (container.GetAttachmentsInSlot(mutexSlot).Any() && container.TryGetSlotProperty(mutexSlot, out var slotProperty2))
				{
					string item2 = ((!slotProperty2.label.NullOrEmpty()) ? slotProperty2.label : slotProperty2.defName);
					if (!list.Contains(item2))
					{
						list.Add(item2);
					}
				}
			}
		}
		if (list.Any())
		{
			reason = "被 '" + string.Join(", ", list) + "' 槽禁用";
			return true;
		}
		reason = "";
		return false;
	}

	private void DrawAttachmentsPanel(Rect rect)
	{
		Widgets.DrawMenuSection(rect);
		List<SlotPropertyDef> list = weapon.AllSlotProperties.ToList();
		float num = (float)(list.Count + 1) * 28f;
		foreach (IAttachable allInstalledAttachment in weapon.AllInstalledAttachments)
		{
			if (allInstalledAttachment is ISlotContainer slotContainer && expandedNodes.Contains(slotContainer))
			{
				num += CalculateSubNodeHeight(slotContainer);
			}
		}
		Rect viewRect = new Rect(0f, 0f, rect.width - 16f, num);
		Widgets.BeginScrollView(rect.ContractedBy(4f), ref attachmentsScrollPosition, viewRect);
		float curY = 0f;
		DrawUnifiedNodeFor(viewRect, ref curY, weapon, 0);
		Widgets.EndScrollView();
	}

	private float CalculateSubNodeHeight(ISlotContainer container)
	{
		float num = (float)container.AllSlotProperties.Count() * 28f;
		foreach (IAttachable allInstalledAttachment in container.AllInstalledAttachments)
		{
			if (allInstalledAttachment is ISlotContainer slotContainer && expandedNodes.Contains(slotContainer))
			{
				num += CalculateSubNodeHeight(slotContainer);
			}
		}
		return num;
	}

	private void DrawUnifiedNodeFor(Rect viewRect, ref float curY, ISlotContainer container, int indentLevel)
	{
		if (container == weapon)
		{
			Rect rect = new Rect(0f, curY, viewRect.width, 28f);
			Text.Anchor = TextAnchor.MiddleCenter;
			Widgets.Label(rect, "--- 武器槽位 ---");
			Text.Anchor = TextAnchor.UpperLeft;
			curY += 28f;
		}
		List<SlotPropertyDef> list = container.AllSlotProperties.OrderBy((SlotPropertyDef s) => s.displayOrder).ToList();
		SlottableDefExtension slottableDefExtension = (container as SSR_CustomWeapon)?.def.GetModExtension<SlottableDefExtension>();
		foreach (SlotPropertyDef slotProp in list)
		{
			if (slotProp == null)
			{
				continue;
			}
			Rect rect2 = new Rect(0f, curY, viewRect.width, 28f);
			curY += 28f;
			Rect rect3 = rect2;
			string reason;
			bool flag = IsSlotDisabled(container, slotProp, out reason);
			IAttachable installed = container.GetAttachmentsInSlot(slotProp.defName).FirstOrDefault();
			if (installed == null && slottableDefExtension != null)
			{
				WeaponSlotData weaponSlotData = slottableDefExtension.slots?.FirstOrDefault((WeaponSlotData s) => s.slotDef == slotProp.defName);
				if (weaponSlotData?.visuals != null)
				{
					float num = 80f;
					Rect rect4 = new Rect(rect2.xMax - num, rect2.y, num, rect2.height);
					rect3.width -= num;
					if (Widgets.ButtonText(rect4, "调试外观"))
					{
						HandleDefaultPartDebugClick(slotProp.defName, weaponSlotData.visuals);
					}
				}
			}
			string text = (slotProp.label.NullOrEmpty() ? slotProp.defName : slotProp.label);
			string text2;
			if (installed != null)
			{
				text2 = text + ": " + installed.LabelCap;
			}
			else
			{
				text2 = text + ": (空)";
				if (flag)
				{
					text2 = text2 + " [" + reason + "]";
				}
			}
			bool flag2 = installed is ISlotContainer;
			bool isExpanded = flag2 && expandedNodes.Contains((ISlotContainer)installed);
			if (flag2)
			{
				text2 = (isExpanded ? "▼ " : "► ") + text2;
			}
			DrawAttachmentRow(clickAction: (!flag2) ? ((Action)delegate
			{
				HandleSlotClick(container, slotProp, installed);
			}) : ((Action)delegate
			{
				if (isExpanded)
				{
					expandedNodes.Remove((ISlotContainer)installed);
				}
				else
				{
					expandedNodes.Add((ISlotContainer)installed);
				}
				SoundDefOf.Click.PlayOneShotOnCamera();
			}), rect: rect3, label: text2, indentLevel: indentLevel, isContainer: flag2, parent: container, slot: slotProp, attachment: installed, isDisabled: flag);
			if (flag2 && isExpanded)
			{
				DrawUnifiedNodeFor(viewRect, ref curY, (ISlotContainer)installed, indentLevel + 1);
			}
		}
	}

	private void HandleDefaultPartDebugClick(string slotKey, WeaponSlotData.Visuals visuals)
	{
		LeafAttachment att = new LeafAttachment
		{
			def = new AttachmentDef
			{
				label = slotKey
			},
			useDebugValues = true,
			debugOffset = visuals.offset,
			debugScale = new Vector3(visuals.scale.x, 1f, visuals.scale.y),
			debugRotation = Vector3.zero
		};
		Action<AttachmentBase> callback = delegate(AttachmentBase closedAtt)
		{
			visuals.offset = closedAtt.debugOffset;
			visuals.scale = new Vector2(closedAtt.debugScale.x, closedAtt.debugScale.z);
			weapon.InvalidateGraphicCache();
		};
		Dialog_WeaponAttachmentDebug window = new Dialog_WeaponAttachmentDebug(att, weapon, callback, CanvasTexture);
		Find.WindowStack.Add(window);
	}

	private void DrawAttachmentRow(Rect rect, string label, Action clickAction, int indentLevel, bool isContainer, ISlotContainer parent, SlotPropertyDef slot, IAttachable attachment, bool isDisabled)
	{
		float num = (float)indentLevel * 15f;
		rect.xMin += num;
		if (Mouse.IsOver(rect))
		{
			Widgets.DrawHighlight(rect);
		}
		Color color = GUI.color;
		if (isDisabled && attachment == null)
		{
			GUI.color = Color.gray;
		}
		bool flag = Widgets.ButtonText(rect, label, drawBackground: false, doMouseoverSound: true, !isDisabled);
		GUI.color = color;
		if (flag)
		{
			if (isContainer && Event.current.shift)
			{
				HandleSlotClick(parent, slot, attachment);
			}
			else
			{
				clickAction?.Invoke();
			}
		}
		if (Widgets.ButtonInvisible(rect, doMouseoverSound: false) && Event.current.button == 1 && !isDisabled)
		{
			HandleSlotClick(parent, slot, attachment);
		}
	}

	private void DrawPreviewPanel(Rect rect)
	{
		Widgets.DrawBox(rect);
		Widgets.ThingIcon(rect.ContractedBy(10f), weapon);
		Text.Font = GameFont.Medium;
		Rect rect2 = new Rect(rect.x, rect.yMax - 60f, rect.width, 30f);
		Text.Anchor = TextAnchor.UpperCenter;
		Widgets.Label(rect2, weapon.LabelCap);
		Text.Font = GameFont.Small;
		Rect rect3 = new Rect(rect.x, rect.yMax - 30f, rect.width, 30f);
		GUI.color = Color.gray;
		Widgets.Label(rect3, weapon.def.description);
		GUI.color = Color.white;
		Text.Anchor = TextAnchor.UpperLeft;
	}

	private void DrawStatsPanel(Rect rect)
	{
		Widgets.DrawMenuSection(rect);
		AttachmentStatCache statCache = weapon.StatCache;
		statCache.EnsureValid();
		List<StatDef> list = statCache._cachedStats.Keys.ToList();
		list.Add(StatDefOf.Mass);
		list.Add(StatDefOf.MarketValue);
		list = (from s in list.Distinct()
			orderby s.category.displayOrder, s.displayPriorityInCategory
			select s).ToList();
		Verb verb = weapon.GetComp<CompEquippable>()?.PrimaryVerb;
		float num = ((verb != null) ? 120f : 0f);
		float height = (float)list.Count * Text.LineHeight + num + 50f;
		Rect rect2 = new Rect(0f, 0f, rect.width - 16f, height);
		Widgets.BeginScrollView(rect.ContractedBy(4f), ref statsScrollPosition, rect2);
		Listing_Standard listing_Standard = new Listing_Standard();
		listing_Standard.Begin(rect2);
		listing_Standard.Label("--- 武器属性 ---");
		foreach (StatDef item in list)
		{
			DrawStatRow(listing_Standard, item);
		}
		if (verb != null)
		{
			listing_Standard.GapLine();
			listing_Standard.Label("--- 射击属性 ---");
			DrawVerbStats(listing_Standard, verb);
		}
		listing_Standard.End();
		Widgets.EndScrollView();
	}

	private void DrawStatRow(Listing_Standard listing, StatDef stat)
	{
		float valueUnfinalized = stat.Worker.GetValueUnfinalized(StatRequest.For(weapon.def, weapon.Stuff));
		float statValue = weapon.GetStatValue(stat);
		float num = statValue - valueUnfinalized;
		if (Mathf.Approximately(num, 0f) && valueUnfinalized == statValue)
		{
			return;
		}
		string text = stat.LabelCap;
		string text2 = statValue.ToStringByStyle(stat.toStringStyle);
		string text3 = "";
		if (!Mathf.Approximately(num, 0f))
		{
			text3 = $" ({num:+#,0.##;-#,0.##;0})";
			if (num > 0f)
			{
				text3 = text3.Colorize(Color.green);
			}
			if (num < 0f)
			{
				text3 = text3.Colorize(Color.red);
			}
		}
		Rect rect = listing.GetRect(Text.LineHeight);
		Widgets.Label(rect, text + ": " + text2 + text3);
		TooltipHandler.TipRegion(rect, "基础值: " + valueUnfinalized.ToStringByStyle(stat.toStringStyle) + "\n\n" + stat.description);
	}

	private void DrawVerbStats(Listing_Standard listing, Verb primaryVerb)
	{
		listing.Label($"伤害: {primaryVerb.verbProps.defaultProjectile.projectile.GetDamageAmount(weapon)}");
		listing.Label($"射程: {primaryVerb.verbProps.range}");
		listing.Label($"连射数: {primaryVerb.BurstShotCount}");
		listing.Label($"连射间隔: {primaryVerb.TicksBetweenBurstShots}");
	}

	private bool IsCompatible(AttachmentDef attDef, SlotPropertyDef slotProp)
	{
		if (attDef == null || slotProp == null)
		{
			return false;
		}
		HashSet<string> allowedParentSlotTagsSet = attDef.AllowedParentSlotTagsSet;
		IEnumerable<string> tags = slotProp.tags;
		return allowedParentSlotTagsSet.IsSubsetOf(tags ?? Enumerable.Empty<string>());
	}

	private void HandleSlotClick(ISlotContainer parent, SlotPropertyDef slot, IAttachable installedAttachment)
	{
		List<FloatMenuOption> list = new List<FloatMenuOption>();
		if (installedAttachment != null)
		{
			list.Add(new FloatMenuOption("卸载 " + installedAttachment.LabelCap, delegate
			{
				parent.TryUninstall(installedAttachment);
			}));
			AttachmentBase attBase = installedAttachment as AttachmentBase;
			if (attBase != null)
			{
				list.Add(new FloatMenuOption("DEV: 调试外观", delegate
				{
					Find.WindowStack.Add(new Dialog_WeaponAttachmentDebug(attBase, weapon, null, CanvasTexture));
				}));
			}
		}
		else
		{
			IEnumerable<AttachmentDef> source = DefDatabase<AttachmentDef>.AllDefs.Where((AttachmentDef def) => IsCompatible(def, slot));
			if (parent.GetAttachmentsInSlot(slot.defName).Count() >= slot.capacity)
			{
				list.Add(new FloatMenuOption("槽位已满", null));
			}
			else if (source.Any())
			{
				foreach (AttachmentDef attDef in source.OrderBy((AttachmentDef d) => d.label))
				{
					list.Add(new FloatMenuOption("安装 " + attDef.label, delegate
					{
						IAttachable item = AttachmentFactory.MakeAttachment(attDef);
						parent.TryInstall(slot.defName, item);
					}));
				}
			}
			else
			{
				list.Add(new FloatMenuOption("无兼容附件", null));
			}
		}
		if (list.Any())
		{
			Find.WindowStack.Add(new FloatMenu(list));
		}
	}
}
