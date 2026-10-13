using System.Collections.Generic;
using System.Linq;
using System.Text;
using FS_SSR_Combat.CustomArmor;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

[StaticConstructorOnStartup]
public class Gizmo_HealthPanel : Gizmo
{
	private readonly SSR_CustomArmor armor;

	private const float PanelWidth = 85f;

	private const float PanelHeight = 75f;

	private const float Padding = 4f;

	private static readonly Texture2D HealthBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.8f, 0.24f));

	private static readonly Texture2D BarBgTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.25f, 0.25f, 0.25f));

	public Gizmo_HealthPanel(SSR_CustomArmor armor)
	{
		this.armor = armor;
		Order = -100f;
	}

	public override float GetWidth(float maxWidth)
	{
		int num = armor.AllInstalledAttachments.Count((IAttachable att) => (att.def.apparelProperties?.maxHP ?? (-1f)) >= 0f);
		return (float)num * 85f;
	}

	public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
	{
		if (armor == null)
		{
			return new GizmoResult(GizmoState.Clear);
		}
		Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
		Widgets.DrawWindowBackground(rect);
		List<IAttachable> list = armor.AllInstalledAttachments.Where((IAttachable attachable) => (attachable.def.apparelProperties?.maxHP ?? (-1f)) >= 0f).ToList();
		float num = rect.x;
		foreach (IAttachable item in list)
		{
			if (item is AttachmentBase att)
			{
				Rect panelRect = new Rect(num, rect.y, 85f, 75f);
				DrawHealthPanelFor(panelRect, att);
				num += 85f;
			}
		}
		return new GizmoResult(GizmoState.Clear);
	}

	private void DrawHealthPanelFor(Rect panelRect, AttachmentBase att)
	{
		Rect rect = new Rect(panelRect.x, panelRect.y + 4f, panelRect.width, 22f);
		Text.Font = GameFont.Tiny;
		Text.Anchor = TextAnchor.UpperCenter;
		Widgets.Label(rect, att.LabelCap);
		Text.Anchor = TextAnchor.UpperLeft;
		float width = panelRect.width - 8f;
		Rect rect2 = new Rect(panelRect.x + 4f, rect.yMax, width, 24f);
		Widgets.FillableBar(rect2, att.HP / att.MaxHP, HealthBarTex, BarBgTex, doBorder: true);
		Text.Font = GameFont.Small;
		Text.Anchor = TextAnchor.MiddleCenter;
		Widgets.Label(rect2, $"{att.HP:F0} / {att.MaxHP:F0}");
		Text.Anchor = TextAnchor.UpperLeft;
		TooltipHandler.TipRegion(panelRect, () => GetAttachmentTooltip(att), att.UniqueID);
	}

	private string GetAttachmentTooltip(AttachmentBase att)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine(att.LabelCap);
		stringBuilder.AppendLine("---");
		stringBuilder.AppendLine(string.Format("{0}: {1:F0} / {2:F0} ({3:P0})", "Integrity".Translate(), att.HP, att.MaxHP, att.HP / att.MaxHP));
		if (att is ISlotContainer container)
		{
			AddChildAttachmentsToTooltip(container, stringBuilder, 1);
		}
		return stringBuilder.ToString();
	}

	private void AddChildAttachmentsToTooltip(ISlotContainer container, StringBuilder sb, int indentLevel)
	{
		if (!container.AllInstalledAttachments.Any())
		{
			return;
		}
		string arg = new string(' ', indentLevel * 2);
		foreach (IAttachable allInstalledAttachment in container.AllInstalledAttachments)
		{
			sb.AppendLine($"{arg}- {allInstalledAttachment.def.LabelCap}");
			if (allInstalledAttachment is ISlotContainer container2)
			{
				AddChildAttachmentsToTooltip(container2, sb, indentLevel + 1);
			}
		}
	}
}
