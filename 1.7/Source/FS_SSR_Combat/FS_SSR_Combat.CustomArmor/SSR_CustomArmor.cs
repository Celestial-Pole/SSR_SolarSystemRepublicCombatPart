using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.Attachments.ModExtension;
using RimWorld;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.CustomArmor;

public class SSR_CustomArmor : Apparel, ISlotContainer
{
	private class RenderInfo
	{
		public Graphic graphic;

		public ApparelLayerDef layer;

		public ApparelGraphicProperties props;
	}

	private Dictionary<string, List<IAttachable>> _installedAttachments;

	private AttachmentStatCache _statCache;

	private HashSet<IAttachable> _tickableChildren;

	private Dictionary<BodyPartDef, List<IAttachable>> _coverageCache;

	private bool _isCoverageCacheDirty = true;

	private List<BodyPartRecord> _pawnBodyParts;

	private bool _resourcePoolsMightBeDirty = true;

	private Dictionary<ResourceDef, ResourcePool> _resourcePools;

	private List<IAttachable> _renderableAttachmentsCache;

	private Dictionary<IAttachable, RenderInfo> _renderInfoCache;

	private bool _isGraphicCacheDirty = true;

	public Thing RootHost => this;

	public Pawn Holder => base.Wearer;

	public AttachmentStatCache StatCache => _statCache;

	public IEnumerable<SlotPropertyDef> AllSlotProperties
	{
		get
		{
			SlottableDefExtension modExtension = def.GetModExtension<SlottableDefExtension>();
			if (modExtension?.slots == null)
			{
				yield break;
			}
			foreach (WeaponSlotData slot in modExtension.slots)
			{
				SlotPropertyDef named = DefDatabase<SlotPropertyDef>.GetNamed(slot.slotDef, errorOnFail: false);
				if (named != null)
				{
					yield return named;
				}
			}
		}
	}

	public IEnumerable<IAttachable> AllInstalledAttachments => _installedAttachments.Values.SelectMany((List<IAttachable> list) => list);

	public SSR_CustomArmor()
	{
		_installedAttachments = new Dictionary<string, List<IAttachable>>();
		_statCache = new AttachmentStatCache(this);
		_tickableChildren = new HashSet<IAttachable>();
		_coverageCache = new Dictionary<BodyPartDef, List<IAttachable>>();
		_resourcePools = new Dictionary<ResourceDef, ResourcePool>();
		InitializeResourcePools();
	}

	public override void PostMake()
	{
		base.PostMake();
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
		IAttachable attachable = GetAttachmentsInSlot(slotId).FirstOrDefault();
		if (attachable != null && !TryUninstallInternal(attachable, isPartOfReplacement: true))
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
		return TryUninstallInternal(item, isPartOfReplacement: false);
	}

	private bool TryUninstallInternal(IAttachable item, bool isPartOfReplacement)
	{
		if (item == null || item.SlotId == null)
		{
			return false;
		}
		if (!isPartOfReplacement && TryGetSlotProperty(item.SlotId, out var slotProperty) && slotProperty.isEssential)
		{
			Messages.Message("CannotUninstallEssentialPart_MustReplace".Translate(item.LabelCap, slotProperty.LabelCap), MessageTypeDefOf.RejectInput);
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
		if (tickable != null)
		{
			_tickableChildren.Add(tickable);
		}
	}

	public void DeregisterTickable(IAttachable tickable)
	{
		if (tickable != null)
		{
			_tickableChildren.Remove(tickable);
		}
	}

	public void Notify(IAttachable source, AttachmentNotificationType type)
	{
		switch (type)
		{
		case AttachmentNotificationType.StatsChanged:
		case AttachmentNotificationType.StateChanged:
			_statCache?.MarkDirty();
			InvalidateCoverageCache();
			InvalidateGraphicCache();
			_resourcePoolsMightBeDirty = true;
			break;
		case AttachmentNotificationType.GraphicDirty:
			InvalidateGraphicCache();
			break;
		case AttachmentNotificationType.HealthChanged:
		case AttachmentNotificationType.EnergyChanged:
			break;
		}
	}

	public bool TryGetResourcePool(ResourceDef def, out ResourcePool pool)
	{
		EnsureResourcePoolsInitialized();
		return _resourcePools.TryGetValue(def, out pool);
	}

	private void EnsureResourcePoolsInitialized()
	{
		if (_resourcePools.Count != DefDatabase<ResourceDef>.DefCount)
		{
			InitializeResourcePools();
		}
	}

	private void InitializeResourcePools()
	{
		foreach (ResourceDef allDef in DefDatabase<ResourceDef>.AllDefs)
		{
			if (!_resourcePools.ContainsKey(allDef))
			{
				_resourcePools[allDef] = new ResourcePool(allDef);
			}
		}
	}

	private void RecalculateResourcePoolsIfNeeded()
	{
		EnsureResourcePoolsInitialized();
		RecalculateAllResourcePools();
	}

	private void RecalculateAllResourcePools()
	{
		foreach (ResourcePool value2 in _resourcePools.Values)
		{
			value2.RecalculateMaxValue(0f);
		}
		Dictionary<ResourceDef, float> dictionary = new Dictionary<ResourceDef, float>();
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			CollectResourceCapacitiesRecursive(allInstalledAttachment, dictionary, isParentFunctionallyDisabled: false);
		}
		foreach (KeyValuePair<ResourceDef, float> item in dictionary)
		{
			if (_resourcePools.TryGetValue(item.Key, out var value))
			{
				value.RecalculateMaxValue(item.Value);
			}
		}
	}

	private void CollectResourceCapacitiesRecursive(IAttachable currentItem, Dictionary<ResourceDef, float> capacities, bool isParentFunctionallyDisabled)
	{
		if (currentItem == null)
		{
			return;
		}
		bool flag = currentItem is AttachmentBase attachmentBase && attachmentBase.IsBroken;
		bool flag2 = isParentFunctionallyDisabled || flag;
		if (!flag2 && currentItem.def.resourceCapacityModifiers != null)
		{
			foreach (ResourceCapacityModifier resourceCapacityModifier in currentItem.def.resourceCapacityModifiers)
			{
				float num = resourceCapacityModifier.value;
				if (currentItem is CompositeAttachment compositeAttachment)
				{
					num += compositeAttachment.GetInternalStatValue(AttachmentStatDefOf.ResourceCapacity, resourceCapacityModifier.resource);
				}
				if (!capacities.ContainsKey(resourceCapacityModifier.resource))
				{
					capacities[resourceCapacityModifier.resource] = 0f;
				}
				capacities[resourceCapacityModifier.resource] += num;
			}
		}
		if (!(currentItem is ISlotContainer slotContainer))
		{
			return;
		}
		bool isParentFunctionallyDisabled2 = flag2 && (currentItem.def.failureLogic?.childAttachmentsFailWhenBroken ?? true);
		foreach (IAttachable allInstalledAttachment in slotContainer.AllInstalledAttachments)
		{
			CollectResourceCapacitiesRecursive(allInstalledAttachment, capacities, isParentFunctionallyDisabled2);
		}
	}

	public bool TryGetAttachmentStatEffects(StatDef stat, out float offset, out float factor)
	{
		offset = _statCache.GetStatOffset(stat);
		factor = _statCache.GetStatFactor(stat);
		if (Mathf.Approximately(offset, 0f))
		{
			return !Mathf.Approximately(factor, 1f);
		}
		return true;
	}

	public float GetAttachmentsArmorRatingForPart(StatDef stat, List<IAttachable> coveringAttachments)
	{
		float num = 0f;
		if (coveringAttachments.NullOrEmpty())
		{
			return 0f;
		}
		foreach (IAttachable coveringAttachment in coveringAttachments)
		{
			num += GetSingleAttachmentArmorRating(stat, coveringAttachment);
		}
		return num;
	}

	public float DistributeDamageToAttachments(float totalDamage, DamageInfo dinfo, List<IAttachable> coveringAttachments, StringBuilder logBuilder = null)
	{
		logBuilder?.AppendLine("--- Damage Absorption Phase ---");
		float num = totalDamage;
		foreach (IAttachable coveringAttachment in coveringAttachments)
		{
			if (num <= 0f)
			{
				break;
			}
			DamageInfo dinfo2 = dinfo;
			dinfo2.SetAmount(num);
			coveringAttachment.PreApplyDamage(ref dinfo2, out var absorbed);
			num = dinfo2.Amount;
			if (absorbed)
			{
				logBuilder?.AppendLine($"  - Damage fully absorbed by a hook on '{coveringAttachment.def.LabelCap}'. Remaining damage is now: {num:F1}");
				continue;
			}
			AttachmentDef.ApparelProperties apparelProperties = coveringAttachment.def.apparelProperties;
			if (apparelProperties != null && apparelProperties.takesDamage && coveringAttachment is AttachmentBase { MaxHP: >0f, HP: var hP } attachmentBase)
			{
				float num2 = Mathf.Min(num, hP);
				if (num2 > 0f)
				{
					attachmentBase.HP -= num2;
					num -= num2;
					logBuilder?.AppendLine($"  - '{attachmentBase.LabelCap}' absorbed {num2:F1} damage via HP. Health: {hP:F1} -> {attachmentBase.HP:F1}.");
					coveringAttachment.PostApplyDamage(dinfo, num2);
				}
			}
		}
		logBuilder?.AppendLine($"-> Unabsorbed (overflow) damage: {num:F1}");
		return num;
	}

	public float GetSingleAttachmentArmorRating(StatDef stat, IAttachable attachment)
	{
		if (attachment is ISlotContainer slotContainer)
		{
			return slotContainer.StatCache.GetStatOffset(stat);
		}
		List<StatModifier> list = new List<StatModifier>();
		attachment.CollectSelfStats(list, null);
		float num = 0f;
		foreach (StatModifier item in list)
		{
			if (item.stat == stat)
			{
				num += item.value;
			}
		}
		return num;
	}

	public void InvalidateCoverageCache()
	{
		_isCoverageCacheDirty = true;
	}

	public bool TryGetCoveringAttachments(BodyPartRecord hitPart, out List<IAttachable> coveringAttachments)
	{
		if (base.Wearer == null)
		{
			coveringAttachments = null;
			return false;
		}
		if (_isCoverageCacheDirty)
		{
			RecalculateCoverageCache();
		}
		HashSet<IAttachable> hashSet = new HashSet<IAttachable>();
		for (BodyPartRecord bodyPartRecord = hitPart; bodyPartRecord != null; bodyPartRecord = bodyPartRecord.parent)
		{
			if (_coverageCache.TryGetValue(bodyPartRecord.def, out var value))
			{
				foreach (IAttachable item in value)
				{
					if (AttachmentMatchesSide(item, hitPart))
					{
						hashSet.Add(item);
					}
				}
			}
		}
		if (hashSet.Any())
		{
			coveringAttachments = hashSet.ToList();
			coveringAttachments.SortByDescending((IAttachable att) => att.def.apparelProperties?.coveragePriority ?? 0);
			return true;
		}
		coveringAttachments = null;
		return false;
	}

	private bool AttachmentMatchesSide(IAttachable attachment, BodyPartRecord hitPart)
	{
		if (attachment.Parent == null || attachment.SlotId == null)
		{
			return true;
		}
		if (attachment.Parent.TryGetSlotProperty(attachment.SlotId, out var slotProperty) && slotProperty.tags != null)
		{
			string text = null;
			if (slotProperty.tags.Contains("Side_Left"))
			{
				text = "left";
			}
			else if (slotProperty.tags.Contains("Side_Right"))
			{
				text = "right";
			}
			if (text == null)
			{
				return true;
			}
			for (BodyPartRecord bodyPartRecord = hitPart; bodyPartRecord != null; bodyPartRecord = bodyPartRecord.parent)
			{
				if (bodyPartRecord.untranslatedCustomLabel != null && bodyPartRecord.untranslatedCustomLabel.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}
		return true;
	}

	private void RecalculateCoverageCache()
	{
		_coverageCache.Clear();
		if (base.Wearer == null)
		{
			_isCoverageCacheDirty = false;
			return;
		}
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			CollectCoveringAttachmentsRecursive(allInstalledAttachment, isParentFunctionallyDisabled: false);
		}
		_isCoverageCacheDirty = false;
	}

	private void CollectCoveringAttachmentsRecursive(IAttachable currentItem, bool isParentFunctionallyDisabled)
	{
		if (currentItem == null)
		{
			return;
		}
		bool flag = currentItem is AttachmentBase attachmentBase && attachmentBase.IsBroken;
		bool flag2 = isParentFunctionallyDisabled || flag;
		if (!flag2)
		{
			AttachmentDef.RenderProperties renderProperties = currentItem.def.renderProperties;
			if (renderProperties != null && renderProperties.isRenderable && !string.IsNullOrEmpty(currentItem.def.renderProperties.graphicPath) && currentItem.def.renderProperties.apparelLayer != null)
			{
				foreach (CoverageRule coverageRule in currentItem.def.apparelProperties.coverageRules)
				{
					if (coverageRule.part != null)
					{
						if (!_coverageCache.TryGetValue(coverageRule.part, out var value))
						{
							value = new List<IAttachable>();
							_coverageCache[coverageRule.part] = value;
						}
						if (!value.Contains(currentItem))
						{
							value.Add(currentItem);
						}
					}
				}
			}
		}
		if (!(currentItem is ISlotContainer slotContainer))
		{
			return;
		}
		bool isParentFunctionallyDisabled2 = flag2 && (currentItem.def.failureLogic?.childAttachmentsFailWhenBroken ?? true);
		foreach (IAttachable allInstalledAttachment in slotContainer.AllInstalledAttachments)
		{
			CollectCoveringAttachmentsRecursive(allInstalledAttachment, isParentFunctionallyDisabled2);
		}
	}

	public override void Notify_Equipped(Pawn pawn)
	{
		base.Notify_Equipped(pawn);
		_pawnBodyParts = pawn?.RaceProps.body.AllParts;
		InvalidateCoverageCache();
		InvalidateGraphicCache();
		_statCache.MarkDirty();
	}

	public override void Notify_Unequipped(Pawn pawn)
	{
		base.Notify_Unequipped(pawn);
		InvalidateGraphicCache();
		_pawnBodyParts = null;
		_coverageCache?.Clear();
	}

	public override IEnumerable<Gizmo> GetWornGizmos()
	{
		foreach (Gizmo wornGizmo in base.GetWornGizmos())
		{
			yield return wornGizmo;
		}
		if (Holder != null && AllInstalledAttachments.Any((IAttachable att) => (att.def.apparelProperties?.maxHP ?? (-1f)) >= 0f))
		{
			yield return new Gizmo_HealthPanel(this);
		}
		if (!Prefs.DevMode)
		{
			yield break;
		}
		yield return new Command_Action
		{
			defaultLabel = "DEV: View Armor Status",
			action = delegate
			{
				Find.WindowStack.Add(new Dialog_ArmorStatus(this));
			}
		};
		yield return new Command_Action
		{
			defaultLabel = "DEV: 安装插件...",
			action = delegate
			{
				ShowInstallMenu(this);
			}
		};
		if (AllInstalledAttachments.Any())
		{
			yield return new Command_Action
			{
				defaultLabel = "DEV: 卸载/管理...",
				action = delegate
				{
					ShowManageMenu(this);
				}
			};
		}
	}

	private void ShowInstallMenu(ISlotContainer target)
	{
		List<FloatMenuOption> list = new List<FloatMenuOption>();
		IEnumerable<SlotPropertyDef> enumerable = target.AllSlotProperties.Where(delegate(SlotPropertyDef slotPropertyDef)
		{
			int num = target.GetAttachmentsInSlot(slotPropertyDef.defName).Count();
			return num < slotPropertyDef.capacity;
		});
		if (!enumerable.Any())
		{
			list.Add(new FloatMenuOption("无可用槽位", null));
		}
		else
		{
			foreach (SlotPropertyDef slotProp in enumerable)
			{
				IEnumerable<AttachmentDef> enumerable2 = DefDatabase<AttachmentDef>.AllDefs.Where(delegate(AttachmentDef def)
				{
					HashSet<string> allowedParentSlotTagsSet = def.AllowedParentSlotTagsSet;
					IEnumerable<string> tags = slotProp.tags;
					return allowedParentSlotTagsSet.IsSubsetOf(tags ?? Enumerable.Empty<string>());
				});
				foreach (AttachmentDef attDef in enumerable2)
				{
					list.Add(new FloatMenuOption("在 '" + slotProp.label + "' 中安装 '" + attDef.label + "'", delegate
					{
						IAttachable item = AttachmentFactory.MakeAttachment(attDef);
						target.TryInstall(slotProp.defName, item);
					}));
				}
			}
		}
		Find.WindowStack.Add(new FloatMenu(list));
	}

	private void ShowManageMenu(ISlotContainer target)
	{
		List<FloatMenuOption> options = new List<FloatMenuOption>();
		AddManageOptionsFor(target, (target is SSR_CustomArmor) ? "护甲" : "插件");
		if (!options.Any())
		{
			options.Add(new FloatMenuOption("无可管理项", null));
		}
		Find.WindowStack.Add(new FloatMenu(options));
		void AddManageOptionsFor(ISlotContainer currentTarget, string path)
		{
			foreach (IAttachable attachment in currentTarget.AllInstalledAttachments)
			{
				string text = $"{path} > {attachment.def.LabelCap}";
				options.Add(new FloatMenuOption("卸载: " + text, delegate
				{
					currentTarget.TryUninstall(attachment);
				}));
				ISlotContainer childContainer = attachment as ISlotContainer;
				if (childContainer != null && childContainer.AllSlotProperties.Any((SlotPropertyDef p) => childContainer.GetAttachmentsInSlot(p.defName).Count() < p.capacity))
				{
					options.Add(new FloatMenuOption("为 '" + text + "' 安装子插件...", delegate
					{
						ShowInstallMenu(childContainer);
					}));
				}
				AttachmentDef.RenderProperties renderProperties = attachment.def.renderProperties;
				if (renderProperties != null && renderProperties.isRenderable)
				{
					AttachmentBase attBase = attachment as AttachmentBase;
					if (attBase != null)
					{
						options.Add(new FloatMenuOption("调试外观: " + text, delegate
						{
							Find.WindowStack.Add(new Dialog_AttachmentDebug(attBase));
						}));
					}
				}
				if (attachment is ISlotContainer currentTarget2)
				{
					AddManageOptionsFor(currentTarget2, text);
				}
			}
		}
	}

	public void InvalidateGraphicCache()
	{
		_isGraphicCacheDirty = true;
		base.Wearer?.Drawer.renderer.SetAllGraphicsDirty();
	}

	public override void DrawWornExtras()
	{
		base.DrawWornExtras();
		if (base.Wearer == null || base.Wearer.Dead || !base.Wearer.Spawned)
		{
			return;
		}
		if (_isGraphicCacheDirty)
		{
			RecalculateGraphicCache();
		}
		if (_renderableAttachmentsCache.NullOrEmpty())
		{
			return;
		}
		GraphicMeshSet humanlikeBodySetForPawn = HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(base.Wearer);
		GraphicMeshSet humanlikeHeadSetForPawn = HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(base.Wearer);
		foreach (IAttachable item in _renderableAttachmentsCache)
		{
			if (!(item is AttachmentBase attachmentBase) || !_renderInfoCache.TryGetValue(item, out var value))
			{
				continue;
			}
			Graphic graphic = value.graphic;
			Rot4 rotation = base.Wearer.Rotation;
			Vector3 v;
			Vector3 s;
			float z;
			ApparelLayerDef apparelLayerDef;
			if (attachmentBase.useDebugValues)
			{
				v = attachmentBase.debugOffset;
				s = attachmentBase.debugScale;
				z = attachmentBase.debugRotation.z;
				apparelLayerDef = attachmentBase.debugApparelLayer ?? value.layer;
			}
			else
			{
				ApparelGraphicProperties props = value.props;
				v = props?.wornGraphicOffset ?? Vector3.zero;
				s = props?.wornGraphicScale ?? Vector3.one;
				Vector3 obj = props?.wornGraphicRotation ?? Vector3.zero;
				z = obj.z;
				apparelLayerDef = value.layer;
				if (props?.wornGraphicDirectionalOffsets != null)
				{
					v += GetDirectionalOffset(rotation, props.wornGraphicDirectionalOffsets);
				}
			}
			float num = base.Wearer.Drawer.renderer.BodyAngle(PawnRenderFlags.None);
			Vector3 drawPos = base.Wearer.Drawer.DrawPos;
			Vector3 pos = drawPos + v.RotatedBy(num);
			pos.y = drawPos.y + 0.005f + (float)apparelLayerDef.drawOrder / 1000f;
			Quaternion q = Quaternion.AngleAxis(num + z, Vector3.up);
			Material damagedMat = base.Wearer.Drawer.renderer.flasher.GetDamagedMat(graphic.MatAt(rotation));
			Mesh mesh = ((apparelLayerDef == ApparelLayerDefOf.Overhead || apparelLayerDef == ApparelLayerDefOf.EyeCover) ? humanlikeHeadSetForPawn.MeshAt(rotation) : humanlikeBodySetForPawn.MeshAt(rotation));
			Matrix4x4 matrix = Matrix4x4.TRS(pos, q, s);
			Graphics.DrawMesh(mesh, matrix, damagedMat, 0);
		}
	}

	private void RecalculateGraphicCache()
	{
		if (_renderableAttachmentsCache == null)
		{
			_renderableAttachmentsCache = new List<IAttachable>();
		}
		else
		{
			_renderableAttachmentsCache.Clear();
		}
		if (_renderInfoCache == null)
		{
			_renderInfoCache = new Dictionary<IAttachable, RenderInfo>();
		}
		else
		{
			_renderInfoCache.Clear();
		}
		if (base.Wearer == null)
		{
			_isGraphicCacheDirty = false;
			return;
		}
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			CollectRenderableAttachmentsRecursive(allInstalledAttachment, isParentFunctionallyDisabled: false);
		}
		BodyTypeDef bodyType = base.Wearer.story.bodyType;
		foreach (IAttachable item in _renderableAttachmentsCache)
		{
			AttachmentDef attachmentDef = item.def;
			AttachmentDef.RenderProperties renderProperties = attachmentDef.renderProperties;
			if (renderProperties != null)
			{
				RenderInfo value = new RenderInfo
				{
					graphic = GraphicDatabase.Get<Graphic_Multi>(renderProperties.graphicPath, ShaderDatabase.Cutout, Vector2.one, DrawColor),
					layer = renderProperties.apparelLayer,
					props = GetBestBodyTypeGraphic(attachmentDef, bodyType, base.Wearer.gender)
				};
				_renderInfoCache[item] = value;
			}
		}
		_isGraphicCacheDirty = false;
	}

	private void CollectRenderableAttachmentsRecursive(IAttachable currentItem, bool isParentFunctionallyDisabled)
	{
		if (currentItem == null)
		{
			return;
		}
		bool flag = currentItem is AttachmentBase attachmentBase && attachmentBase.IsBroken;
		bool flag2 = isParentFunctionallyDisabled || flag;
		AttachmentDef.RenderProperties renderProperties = currentItem.def.renderProperties;
		if (!flag2 && renderProperties != null && renderProperties.isRenderable && !string.IsNullOrEmpty(renderProperties.graphicPath) && renderProperties.apparelLayer != null)
		{
			_renderableAttachmentsCache.Add(currentItem);
		}
		if (!(currentItem is ISlotContainer slotContainer))
		{
			return;
		}
		bool isParentFunctionallyDisabled2 = flag2 && (currentItem.def.failureLogic?.childAttachmentsFailWhenBroken ?? true);
		foreach (IAttachable allInstalledAttachment in slotContainer.AllInstalledAttachments)
		{
			CollectRenderableAttachmentsRecursive(allInstalledAttachment, isParentFunctionallyDisabled2);
		}
	}

	private ApparelGraphicProperties GetBestBodyTypeGraphic(AttachmentDef def, BodyTypeDef pawnBodyType, Gender pawnGender)
	{
		Dictionary<BodyTypeDef, ApparelGraphicProperties> apparelGraphicsDict = def.ApparelGraphicsDict;
		if (apparelGraphicsDict.NullOrEmpty())
		{
			return null;
		}
		if (apparelGraphicsDict.TryGetValue(pawnBodyType, out var value))
		{
			return value;
		}
		if (pawnGender == Gender.Male && apparelGraphicsDict.TryGetValue(BodyTypeDefOf.Male, out value))
		{
			return value;
		}
		if (pawnGender == Gender.Female && apparelGraphicsDict.TryGetValue(BodyTypeDefOf.Female, out value))
		{
			return value;
		}
		return def.renderProperties?.apparelGraphics?.FirstOrDefault();
	}

	private Vector3 GetDirectionalOffset(Rot4 facing, WornGraphicDirectionalOffsets offsets)
	{
		return facing.AsInt switch
		{
			0 => offsets.north, 
			1 => offsets.east, 
			2 => offsets.south, 
			3 => (offsets.west != Vector3.zero) ? offsets.west : new Vector3(0f - offsets.east.x, offsets.east.y, offsets.east.z), 
			_ => Vector3.zero, 
		};
	}

	protected override void Tick()
	{
		base.Tick();
		if (_resourcePoolsMightBeDirty)
		{
			RecalculateResourcePoolsIfNeeded();
			_resourcePoolsMightBeDirty = false;
		}
		if (!_tickableChildren.Any())
		{
			return;
		}
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
		Scribe_Collections.Look(ref _resourcePools, "resourcePools", LookMode.Def, LookMode.Deep);
		if (Scribe.mode != LoadSaveMode.PostLoadInit)
		{
			return;
		}
		if (_installedAttachments == null)
		{
			_installedAttachments = new Dictionary<string, List<IAttachable>>();
		}
		if (_statCache == null)
		{
			_statCache = new AttachmentStatCache(this);
		}
		if (_tickableChildren == null)
		{
			_tickableChildren = new HashSet<IAttachable>();
		}
		_tickableChildren.Clear();
		foreach (KeyValuePair<string, List<IAttachable>> installedAttachment in _installedAttachments)
		{
			string key = installedAttachment.Key;
			foreach (IAttachable item in installedAttachment.Value)
			{
				item.Notify_HostLoadedAndLinked(this, key);
			}
		}
		_statCache.MarkDirty();
		InvalidateCoverageCache();
		InvalidateGraphicCache();
		_resourcePoolsMightBeDirty = true;
	}
}
