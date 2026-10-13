using System;
using System.Globalization;
using System.Linq;
using System.Text;
using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.CustomWeapon;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.UI;

public class Dialog_WeaponAttachmentDebug : Window
{
	private readonly AttachmentBase attachment;

	private readonly Pawn holder;

	private readonly Action<AttachmentBase> onCloseCallback;

	private readonly SSR_CustomWeapon weaponToUpdate;

	private readonly Texture2D canvasTexture;

	private float sliderMaxRange = 256f;

	private float sliderStep = 1f;

	private readonly string[] offsetBuffers = new string[3];

	private readonly string[] scaleBuffers = new string[3];

	private readonly string[] rotationBuffers = new string[3];

	public override Vector2 InitialSize => new Vector2(450f, 480f);

	public Dialog_WeaponAttachmentDebug(AttachmentBase att)
		: this(att, att.RootHost as SSR_CustomWeapon, null, null)
	{
		if (canvasTexture == null)
		{
			Dialog_SSRWeaponDebugPanel dialog_SSRWeaponDebugPanel = Find.WindowStack.Windows.OfType<Dialog_SSRWeaponDebugPanel>().FirstOrDefault((Dialog_SSRWeaponDebugPanel p) => p.weapon == weaponToUpdate);
		}
	}

	public Dialog_WeaponAttachmentDebug(AttachmentBase att, SSR_CustomWeapon weapon, Action<AttachmentBase> callback, Texture2D canvas)
	{
		attachment = att;
		holder = att.Holder;
		weaponToUpdate = weapon;
		onCloseCallback = callback;
		draggable = true;
		forcePause = false;
		absorbInputAroundWindow = false;
		closeOnClickedOutside = false;
		doCloseX = true;
		preventCameraMotion = false;
		resizeable = true;
		if (canvasTexture != null)
		{
			sliderMaxRange = (float)Mathf.Max(canvasTexture.width, canvasTexture.height) / 2f;
			sliderStep = 1f;
		}
		if (!attachment.useDebugValues)
		{
			InitializeDebugValuesFromXml();
		}
		UpdateBuffers();
	}

	private void InitializeDebugValuesFromXml()
	{
		AttachmentDef.RenderProperties renderProperties = attachment.def.renderProperties;
		if (renderProperties != null)
		{
			attachment.debugOffset = renderProperties.renderOffset;
			attachment.debugScale = renderProperties.renderScale;
		}
		else
		{
			attachment.debugOffset = Vector3.zero;
			attachment.debugScale = Vector3.one;
			attachment.debugRotation = Vector3.zero;
		}
	}

	public override void DoWindowContents(Rect inRect)
	{
		Listing_Standard listing_Standard = new Listing_Standard();
		listing_Standard.Begin(inRect);
		Text.Font = GameFont.Medium;
		listing_Standard.Label("调试附件: " + attachment.LabelCap);
		Text.Font = GameFont.Small;
		listing_Standard.GapLine();
		bool checkOn = attachment.useDebugValues;
		listing_Standard.CheckboxLabeled("启用实时调试值", ref checkOn, "勾选后将使用下面的值进行渲染，而不是XML中的值。");
		if (checkOn != attachment.useDebugValues)
		{
			attachment.useDebugValues = checkOn;
			if (checkOn)
			{
				InitializeDebugValuesFromXml();
				UpdateBuffers();
			}
			NotifyGraphicDirty();
		}
		if (!attachment.useDebugValues)
		{
			listing_Standard.End();
			return;
		}
		listing_Standard.Gap();
		if (holder != null)
		{
			listing_Standard.Label("强制持有者转向 (用于调试)");
			Rect rect = listing_Standard.GetRect(30f);
			float num = rect.width / 4f;
			if (Widgets.ButtonText(new Rect(rect.x, rect.y, num, rect.height), "北"))
			{
				holder.Rotation = Rot4.North;
			}
			if (Widgets.ButtonText(new Rect(rect.x + num, rect.y, num, rect.height), "东"))
			{
				holder.Rotation = Rot4.East;
			}
			if (Widgets.ButtonText(new Rect(rect.x + num * 2f, rect.y, num, rect.height), "南"))
			{
				holder.Rotation = Rot4.South;
			}
			if (Widgets.ButtonText(new Rect(rect.x + num * 3f, rect.y, num, rect.height), "西"))
			{
				holder.Rotation = Rot4.West;
			}
			listing_Standard.Gap();
		}
		listing_Standard.Label("位置偏移 (Offset)");
		DrawVector3Editor(listing_Standard, "X (左右):", "Y (深度/层级):", "Z (上下):", ref attachment.debugOffset, offsetBuffers, 0f - sliderMaxRange, sliderMaxRange, sliderStep);
		listing_Standard.Gap();
		listing_Standard.Label("缩放 (Scale)");
		DrawVector3Editor(listing_Standard, "X (宽度):", "Y (未使用):", "Z (高度):", ref attachment.debugScale, scaleBuffers, 0.1f, 5f, 0.05f);
		listing_Standard.Gap();
		listing_Standard.Label("旋转 (Rotation)");
		DrawVector3Editor(listing_Standard, "X (未使用):", "Y (平面旋转):", "Z (未使用):", ref attachment.debugRotation, rotationBuffers, -180f, 180f, 1f);
		listing_Standard.GapLine();
		if (GUI.changed)
		{
			onCloseCallback?.Invoke(attachment);
			weaponToUpdate?.InvalidateGraphicCache();
		}
		Rect rect2 = listing_Standard.GetRect(30f);
		if (Widgets.ButtonText(rect2.LeftHalf().ContractedBy(2f, 0f), "重置为XML值"))
		{
			InitializeDebugValuesFromXml();
			UpdateBuffers();
			NotifyGraphicDirty();
		}
		if (Widgets.ButtonText(rect2.RightHalf().ContractedBy(2f, 0f), "复制XML到剪贴板"))
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("<!-- For Weapon Attachments, paste into AttachmentDef's <renderProperties> block -->");
			stringBuilder.AppendLine($"<renderOffset>({attachment.debugOffset.x:F3}, {attachment.debugOffset.y:F3}, {attachment.debugOffset.z:F3})</renderOffset>");
			stringBuilder.AppendLine($"<renderScale>({attachment.debugScale.x:F2}, {attachment.debugScale.y:F2}, {attachment.debugScale.z:F2})</renderScale>");
			if (attachment.debugRotation != Vector3.zero)
			{
				stringBuilder.AppendLine($"<renderRotation>({attachment.debugRotation.x:F1}, {attachment.debugRotation.y:F1}, {attachment.debugRotation.z:F1})</renderRotation>");
			}
			GUIUtility.systemCopyBuffer = stringBuilder.ToString();
			Messages.Message("XML snippet copied to clipboard.", MessageTypeDefOf.PositiveEvent);
		}
		listing_Standard.End();
	}

	private void NotifyGraphicDirty()
	{
		if (attachment.RootHost is ISlotContainer slotContainer)
		{
			slotContainer.Notify(attachment, AttachmentNotificationType.GraphicDirty);
		}
	}

	private void UpdateBuffers()
	{
		offsetBuffers[0] = attachment.debugOffset.x.ToString("F3");
		offsetBuffers[1] = attachment.debugOffset.y.ToString("F3");
		offsetBuffers[2] = attachment.debugOffset.z.ToString("F3");
		scaleBuffers[0] = attachment.debugScale.x.ToString("F2");
		scaleBuffers[1] = attachment.debugScale.y.ToString("F2");
		scaleBuffers[2] = attachment.debugScale.z.ToString("F2");
		rotationBuffers[0] = attachment.debugRotation.x.ToString("F1");
		rotationBuffers[1] = attachment.debugRotation.y.ToString("F1");
		rotationBuffers[2] = attachment.debugRotation.z.ToString("F1");
	}

	private void DrawVector3Editor(Listing_Standard listing, string labelX, string labelY, string labelZ, ref Vector3 vec, string[] buffers, float min, float max, float step)
	{
		DrawSliderTextField(listing.GetRect(24f), labelX, ref vec.x, ref buffers[0], min, max, step);
		DrawSliderTextField(listing.GetRect(24f), labelY, ref vec.y, ref buffers[1], -10f, 10f, 0.1f);
		DrawSliderTextField(listing.GetRect(24f), labelZ, ref vec.z, ref buffers[2], min, max, step);
	}

	private void DrawSliderTextField(Rect rect, string label, ref float value, ref string buffer, float min, float max, float step)
	{
		float num = value;
		Rect rect2 = rect.LeftPartPixels(110f);
		Widgets.Label(rect2, label);
		Rect rect3 = rect.RightPartPixels(55f);
		string text = buffer;
		buffer = Widgets.TextField(rect3, buffer);
		if (buffer != text && float.TryParse(buffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			value = result;
		}
		Rect rect4 = new Rect(rect2.xMax + 5f, rect.y, rect.width - 110f - 55f - 10f, rect.height);
		value = Widgets.HorizontalSlider(rect4, value, min, max);
		if (Mathf.Abs(value - num) > step / 2f)
		{
			value = GenMath.RoundTo(value, step);
			buffer = value.ToString((step < 0.1f) ? "F3" : ((step < 1f) ? "F2" : "F1"));
			GUI.changed = true;
		}
	}

	public override void PostClose()
	{
		base.PostClose();
		onCloseCallback?.Invoke(attachment);
	}
}
