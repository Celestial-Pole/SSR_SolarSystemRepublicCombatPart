using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

public class Dialog_AttachmentDebug : Window
{
	private AttachmentBase attachment;

	private Pawn wearer;

	private string[] offsetBuffers = new string[3];

	private string[] scaleBuffers = new string[3];

	private string[] rotationBuffers = new string[3];

	private List<(ApparelLayerDef def, string label)> allApparelLayers;

	public override Vector2 InitialSize => new Vector2(1000f, 1000f);

	public Dialog_AttachmentDebug(AttachmentBase att)
	{
		attachment = att;
		wearer = att.Holder;
		if (wearer == null)
		{
			Log.Warning("Dialog_AttachmentDebug opened for an attachment on un-worn armor. This may lead to unexpected behavior.");
		}
		draggable = true;
		forcePause = false;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = false;
		doCloseX = true;
		preventCameraMotion = false;
		resizeable = true;
		InitializeApparelLayerList();
		if (!attachment.useDebugValues)
		{
			InitializeDebugValuesFromXml();
		}
		AttachmentBase attachmentBase = attachment;
		if (attachmentBase.debugApparelLayer == null)
		{
			attachmentBase.debugApparelLayer = attachment.def.renderProperties?.apparelLayer;
		}
		UpdateBuffers();
	}

	private void InitializeApparelLayerList()
	{
		allApparelLayers = new List<(ApparelLayerDef, string)>();
		foreach (ApparelLayerDef item in DefDatabase<ApparelLayerDef>.AllDefs.OrderBy((ApparelLayerDef d) => d.drawOrder))
		{
			string text = item.LabelCap;
			if (item.modContentPack?.PackageId != null && !item.modContentPack.PackageId.StartsWith("ludeon.rimworld"))
			{
				text += " (非原版)";
			}
			allApparelLayers.Add((item, text));
		}
	}

	private ApparelGraphicProperties GetBestGraphicPropsForPawn()
	{
		if (wearer != null)
		{
			AttachmentDef.RenderProperties renderProperties = attachment.def.renderProperties;
			if (renderProperties == null || !renderProperties.apparelGraphics.NullOrEmpty())
			{
				AttachmentDef def = attachment.def;
				BodyTypeDef bodyType = wearer.story.bodyType;
				Dictionary<BodyTypeDef, ApparelGraphicProperties> apparelGraphicsDict = def.ApparelGraphicsDict;
				if (apparelGraphicsDict.TryGetValue(bodyType, out var value))
				{
					return value;
				}
				if (wearer.gender == Gender.Male && apparelGraphicsDict.TryGetValue(BodyTypeDefOf.Male, out value))
				{
					return value;
				}
				if (wearer.gender == Gender.Female && apparelGraphicsDict.TryGetValue(BodyTypeDefOf.Female, out value))
				{
					return value;
				}
				return def.renderProperties.apparelGraphics.FirstOrDefault();
			}
		}
		return null;
	}

	private void InitializeDebugValuesFromXml()
	{
		ApparelGraphicProperties bestGraphicPropsForPawn = GetBestGraphicPropsForPawn();
		attachment.debugOffset = bestGraphicPropsForPawn?.wornGraphicOffset ?? Vector3.zero;
		attachment.debugScale = bestGraphicPropsForPawn?.wornGraphicScale ?? Vector3.one;
		attachment.debugRotation = bestGraphicPropsForPawn?.wornGraphicRotation ?? Vector3.zero;
		attachment.debugApparelLayer = attachment.def.renderProperties?.apparelLayer;
	}

	public override void DoWindowContents(Rect inRect)
	{
		Listing_Standard listing_Standard = new Listing_Standard();
		listing_Standard.Begin(inRect);
		Text.Font = GameFont.Medium;
		listing_Standard.Label("调试: " + attachment.LabelCap);
		Text.Font = GameFont.Small;
		listing_Standard.GapLine();
		bool checkOn = attachment.useDebugValues;
		listing_Standard.CheckboxLabeled("启用实时调试", ref checkOn, "勾选后将使用下面的值进行渲染，而不是XML中的值。");
		if (checkOn != attachment.useDebugValues)
		{
			attachment.useDebugValues = checkOn;
			if (checkOn)
			{
				InitializeDebugValuesFromXml();
				UpdateBuffers();
			}
		}
		if (!attachment.useDebugValues)
		{
			listing_Standard.End();
			return;
		}
		listing_Standard.Gap();
		string text = ((wearer != null) ? wearer.story.bodyType.defName : "N/A");
		listing_Standard.Label("当前Pawn体型: " + text);
		listing_Standard.Gap();
		listing_Standard.Label("渲染图层 (Apparel Layer)");
		Rect rect = listing_Standard.GetRect(30f);
		string label = allApparelLayers.FirstOrDefault(((ApparelLayerDef def, string label) l) => l.def == attachment.debugApparelLayer).label ?? "选择图层";
		if (Widgets.ButtonText(rect, label))
		{
			List<FloatMenuOption> list = new List<FloatMenuOption>();
			foreach (var layerInfo in allApparelLayers)
			{
				list.Add(new FloatMenuOption(layerInfo.label, delegate
				{
					attachment.debugApparelLayer = layerInfo.def;
					wearer?.Drawer.renderer.SetAllGraphicsDirty();
				}));
			}
			Find.WindowStack.Add(new FloatMenu(list));
		}
		listing_Standard.Gap();
		listing_Standard.Label("强制转向 (用于调试)");
		Rect rect2 = listing_Standard.GetRect(30f);
		float num = rect2.width / 4f;
		if (Widgets.ButtonText(new Rect(rect2.x, rect2.y, num, rect2.height), "后 (北)") && wearer != null)
		{
			wearer.Rotation = Rot4.North;
		}
		if (Widgets.ButtonText(new Rect(rect2.x + num, rect2.y, num, rect2.height), "右 (东)") && wearer != null)
		{
			wearer.Rotation = Rot4.East;
		}
		if (Widgets.ButtonText(new Rect(rect2.x + num * 2f, rect2.y, num, rect2.height), "前 (南)") && wearer != null)
		{
			wearer.Rotation = Rot4.South;
		}
		if (Widgets.ButtonText(new Rect(rect2.x + num * 3f, rect2.y, num, rect2.height), "左 (西)") && wearer != null)
		{
			wearer.Rotation = Rot4.West;
		}
		listing_Standard.Gap();
		listing_Standard.Label("位置偏移 (Offset)");
		DrawVector3Editor(listing_Standard, "X (左右):", "Y (深度):", "Z (前后):", ref attachment.debugOffset, offsetBuffers, -2f, 2f, 0.001f);
		listing_Standard.Gap();
		listing_Standard.Label("缩放 (Scale)");
		DrawVector3Editor(listing_Standard, "X:", "Y:", "Z:", ref attachment.debugScale, scaleBuffers, 0.1f, 5f, 0.01f);
		listing_Standard.Gap();
		listing_Standard.Label("旋转 (Rotation) - Z轴最常用");
		DrawVector3Editor(listing_Standard, "X (上下翻):", "Y (左右转):", "Z (倾斜):", ref attachment.debugRotation, rotationBuffers, -180f, 180f, 1f);
		listing_Standard.GapLine();
		Rect rect3 = listing_Standard.GetRect(30f);
		if (Widgets.ButtonText(rect3.LeftHalf().ContractedBy(2f, 0f), "重置为XML值"))
		{
			InitializeDebugValuesFromXml();
			UpdateBuffers();
			wearer?.Drawer.renderer.SetAllGraphicsDirty();
		}
		if (Widgets.ButtonText(rect3.RightHalf().ContractedBy(2f, 0f), "复制XML到剪贴板"))
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("<!-- Copy and paste this into your AttachmentDef's <renderProperties> block -->");
			if (attachment.debugApparelLayer != null)
			{
				stringBuilder.AppendLine("<apparelLayer>" + attachment.debugApparelLayer.defName + "</apparelLayer>");
				stringBuilder.AppendLine("<apparelGraphics>");
			}
			stringBuilder.AppendLine("  <li>");
			stringBuilder.AppendLine("    <bodyType>" + text + "</bodyType>");
			stringBuilder.AppendLine($"    <wornGraphicOffset>({attachment.debugOffset.x:F3}, {attachment.debugOffset.y:F3}, {attachment.debugOffset.z:F3})</wornGraphicOffset>");
			stringBuilder.AppendLine($"    <wornGraphicScale>({attachment.debugScale.x:F3}, {attachment.debugScale.y:F3}, {attachment.debugScale.z:F3})</wornGraphicScale>");
			stringBuilder.AppendLine($"    <wornGraphicRotation>({attachment.debugRotation.x:F1}, {attachment.debugRotation.y:F1}, {attachment.debugRotation.z:F1})</wornGraphicRotation>");
			stringBuilder.AppendLine("  </li>");
			if (attachment.debugApparelLayer != null)
			{
				stringBuilder.AppendLine("</apparelGraphics>");
			}
			GUIUtility.systemCopyBuffer = stringBuilder.ToString();
			Messages.Message("XML for body type '" + text + "' copied to clipboard.", MessageTypeDefOf.PositiveEvent);
		}
		listing_Standard.End();
	}

	private void UpdateBuffers()
	{
		offsetBuffers[0] = attachment.debugOffset.x.ToString("F3");
		offsetBuffers[1] = attachment.debugOffset.y.ToString("F3");
		offsetBuffers[2] = attachment.debugOffset.z.ToString("F3");
		scaleBuffers[0] = attachment.debugScale.x.ToString("F3");
		scaleBuffers[1] = attachment.debugScale.y.ToString("F3");
		scaleBuffers[2] = attachment.debugScale.z.ToString("F3");
		rotationBuffers[0] = attachment.debugRotation.x.ToString("F1");
		rotationBuffers[1] = attachment.debugRotation.y.ToString("F1");
		rotationBuffers[2] = attachment.debugRotation.z.ToString("F1");
	}

	private void DrawVector3Editor(Listing_Standard listing, string labelX, string labelY, string labelZ, ref Vector3 vec, string[] buffers, float min, float max, float step)
	{
		DrawSliderTextField(listing.GetRect(24f), labelX, ref vec.x, ref buffers[0], min, max, step);
		DrawSliderTextField(listing.GetRect(24f), labelY, ref vec.y, ref buffers[1], -0.5f, 0.5f, 0.001f);
		DrawSliderTextField(listing.GetRect(24f), labelZ, ref vec.z, ref buffers[2], min, max, step);
	}

	private void DrawSliderTextField(Rect rect, string label, ref float value, ref string buffer, float min, float max, float step)
	{
		Rect rect2 = rect.LeftPartPixels(70f);
		Rect rect3 = new Rect(rect2.xMax + 5f, rect.y, rect.width - 70f - 55f - 10f, rect.height);
		Rect rect4 = rect.RightPartPixels(55f);
		Widgets.Label(rect2, label);
		float value2 = Widgets.HorizontalSlider(rect3, value, min, max);
		value = GenMath.RoundTo(value2, step);
		string text = buffer;
		buffer = Widgets.TextField(rect4, buffer);
		if (buffer != text && float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			value = result;
		}
		if (GUI.changed || !value.ToString("F3").Equals(buffer))
		{
			buffer = value.ToString("F3");
		}
	}
}
