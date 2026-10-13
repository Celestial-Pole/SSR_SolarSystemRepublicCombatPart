using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

public class AttachmentDef : Def
{
	public class RenderProperties
	{
		public bool isRenderable = true;

		public string graphicPath;

		public string uiIconPath;

		[NoTranslate]
		public string outlinePath;

		[Header("Apparel Rendering")]
		[Tooltip("仅用于护甲附件。决定其在Pawn身上的渲染层级。")]
		public ApparelLayerDef apparelLayer;

		[Tooltip("仅用于护甲附件。定义在不同身体类型上的具体渲染参数。")]
		public List<ApparelGraphicProperties> apparelGraphics;

		[Tooltip("仅用于武器附件。定义其在武器贴图上的渲染偏移。")]
		public Vector3 renderOffset = Vector3.zero;

		[Tooltip("仅用于武器附件。定义其在武器贴图上的渲染缩放。")]
		public Vector3 renderScale = Vector3.one;
	}

	public class ApparelProperties
	{
		public List<CoverageRule> coverageRules;

		public float maxHP = -1f;

		public bool takesDamage;

		public int coveragePriority;

		public bool hidesHair;
	}

	public class WeaponProperties
	{
		public float burstShotSpeedMultiplier = 1f;

		public float burstShotCountMultiplier = 1f;
	}

	public class FailureLogic
	{
		[Tooltip("当附件损坏时，此列表中的Stat加成将会失效。")]
		public List<StatDef> statsDisabledWhenBroken;

		[Tooltip("若为true，当此附件损坏时，其所有子附件的功能都将完全失效。")]
		public bool childAttachmentsFailWhenBroken = true;
	}

	public Type attachmentClass = typeof(LeafAttachment);

	[Tooltip("定义此附件可以安装到哪些标签的槽位中。")]
	public List<string> allowedParentSlotTags;

	[Tooltip("定义此附件【不能】安装到哪些标签的槽位中。即使allowed标签匹配，只要有一个排除标签匹配，就禁止安装。")]
	public List<string> excludeParentSlotTags;

	[Tooltip("安装此附件时，隐藏武器本体上带有这些标签的部件。")]
	public List<string> hidesBodyParts;

	[Tooltip("如果此附件本身也是一个容器，在这里定义它所拥有的槽位。")]
	public List<SlotPropertyDef> slots;

	public RenderProperties renderProperties;

	public ApparelProperties apparelProperties;

	public WeaponProperties weaponProperties;

	public FailureLogic failureLogic;

	public bool isCoreComponent;

	[Tooltip("对宿主（护甲或武器）的属性进行直接数值加成。")]
	public List<StatModifier> statOffsets;

	[Tooltip("对宿主（护甲或武器）的属性进行百分比乘数加成。")]
	public List<StatModifier> statFactors;

	[Tooltip("对父级附件（如果存在）的内部属性进行修改。")]
	public List<AttachmentStatModifier> parentStatModifiers;

	[Tooltip("此附件能为宿主的资源池增加多少容量。")]
	public List<ResourceCapacityModifier> resourceCapacityModifiers;

	[Tooltip("安装此附件时，为其贡献的资源池增加多少初始值。")]
	public List<ResourceCapacityModifier> initialResourceAmounts;

	public bool tickWhileEquipped;

	public List<ThingDefCountClass> costList;

	public float workToMake;

	[Unsaved(false)]
	private HashSet<string> _allowedParentSlotTagsSet;

	[Unsaved(false)]
	private Dictionary<BodyTypeDef, ApparelGraphicProperties> _apparelGraphicsDict;

	[Unsaved(false)]
	private HashSet<string> _excludeParentSlotTagsSet;

	public HashSet<string> AllowedParentSlotTagsSet
	{
		get
		{
			if (_allowedParentSlotTagsSet == null)
			{
				IEnumerable<string> enumerable = allowedParentSlotTags;
				_allowedParentSlotTagsSet = new HashSet<string>(enumerable ?? Enumerable.Empty<string>());
			}
			return _allowedParentSlotTagsSet;
		}
	}

	public Dictionary<BodyTypeDef, ApparelGraphicProperties> ApparelGraphicsDict
	{
		get
		{
			if (_apparelGraphicsDict == null)
			{
				_apparelGraphicsDict = new Dictionary<BodyTypeDef, ApparelGraphicProperties>();
				if (renderProperties?.apparelGraphics != null)
				{
					foreach (ApparelGraphicProperties apparelGraphic in renderProperties.apparelGraphics)
					{
						if (apparelGraphic.bodyType != null)
						{
							_apparelGraphicsDict[apparelGraphic.bodyType] = apparelGraphic;
						}
					}
				}
			}
			return _apparelGraphicsDict;
		}
	}

	public HashSet<string> ExcludeParentSlotTagsSet
	{
		get
		{
			if (_excludeParentSlotTagsSet == null)
			{
				IEnumerable<string> enumerable = excludeParentSlotTags;
				_excludeParentSlotTagsSet = new HashSet<string>(enumerable ?? Enumerable.Empty<string>());
			}
			return _excludeParentSlotTagsSet;
		}
	}

	public override void ResolveReferences()
	{
		base.ResolveReferences();
		if (!slots.NullOrEmpty() && attachmentClass == typeof(LeafAttachment))
		{
			attachmentClass = typeof(CompositeAttachment);
		}
	}
}
