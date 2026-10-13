using System;
using System.Collections.Generic;
using System.Linq;
using FS_SSR_Combat.Attachments;
using FS_SSR_Combat.Attachments.ModExtension;
using FS_SSR_Combat.UI;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.CustomWeapon;

public class SSR_CustomWeapon : ThingWithComps, ISlotContainer
{
	private Dictionary<string, List<IAttachable>> _installedAttachments;

	private AttachmentStatCache _statCache;

	private HashSet<IAttachable> _tickableChildren;

	private Dictionary<ResourceDef, ResourcePool> _resourcePools;

	private bool _resourcePoolsMightBeDirty = true;

	private Graphic _cachedModularGraphic;

	private bool _isGraphicDirty = true;

	private SlottableDefExtension _unifiedExt;

	private static readonly Dictionary<Texture, Material> materialCache = new Dictionary<Texture, Material>();

	private Graphic _baseGraphic_safe;

	private const float OutlineDepthOffset = -0.01f;

	private SlottableDefExtension UnifiedExt => _unifiedExt ?? (_unifiedExt = def.GetModExtension<SlottableDefExtension>());

	public Pawn Holder => (base.ParentHolder as Pawn_EquipmentTracker)?.pawn;

	public Thing RootHost => this;

	public AttachmentStatCache StatCache => _statCache;

	public IEnumerable<SlotPropertyDef> AllSlotProperties
	{
		get
		{
			if (UnifiedExt?.slots == null)
			{
				yield break;
			}
			foreach (WeaponSlotData slot in UnifiedExt.slots)
			{
				SlotPropertyDef named = DefDatabase<SlotPropertyDef>.GetNamed(slot.slotDef, errorOnFail: false);
				if (named != null)
				{
					yield return named;
				}
			}
		}
	}

	public IEnumerable<IAttachable> AllInstalledAttachments
	{
		get
		{
			if (_installedAttachments == null)
			{
				yield break;
			}
			foreach (List<IAttachable> value in _installedAttachments.Values)
			{
				foreach (IAttachable item in value)
				{
					yield return item;
				}
			}
		}
	}

	public SSR_CustomWeapon()
	{
		_installedAttachments = new Dictionary<string, List<IAttachable>>();
		_statCache = new AttachmentStatCache(this);
		_tickableChildren = new HashSet<IAttachable>();
		_resourcePools = new Dictionary<ResourceDef, ResourcePool>();
	}

	public override void PostMake()
	{
		base.PostMake();
	}

	public bool TryGetSlotProperty(string slotId, out SlotPropertyDef slotProperty)
	{
		slotProperty = DefDatabase<SlotPropertyDef>.GetNamed(slotId, errorOnFail: false);
		return slotProperty != null;
	}

	public IEnumerable<IAttachable> GetAttachmentsInSlot(string slotId)
	{
		if (slotId != null && _installedAttachments.TryGetValue(slotId, out var value))
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
		switch (type)
		{
		case AttachmentNotificationType.StatsChanged:
		case AttachmentNotificationType.StateChanged:
			_statCache?.MarkDirty();
			_resourcePoolsMightBeDirty = true;
			InvalidateGraphicCache();
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
				if (!capacities.ContainsKey(resourceCapacityModifier.resource))
				{
					capacities[resourceCapacityModifier.resource] = 0f;
				}
				capacities[resourceCapacityModifier.resource] += resourceCapacityModifier.value;
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
		if (_resourcePools == null)
		{
			_resourcePools = new Dictionary<ResourceDef, ResourcePool>();
		}
		_tickableChildren.Clear();
		foreach (KeyValuePair<string, List<IAttachable>> installedAttachment in _installedAttachments)
		{
			foreach (IAttachable item in installedAttachment.Value)
			{
				item.Notify_HostLoadedAndLinked(this, installedAttachment.Key);
			}
		}
		_statCache.MarkDirty();
		_resourcePoolsMightBeDirty = true;
		_isGraphicDirty = true;
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach (Gizmo gizmo in base.GetGizmos())
		{
			yield return gizmo;
		}
		foreach (Gizmo item in GenerateCustomGizmos())
		{
			yield return item;
		}
	}

	public IEnumerable<Gizmo> GetWornGizmos()
	{
		return GenerateCustomGizmos();
	}

	private IEnumerable<Gizmo> GenerateCustomGizmos()
	{
		if (Prefs.DevMode)
		{
			yield return new Command_Action
			{
				defaultLabel = "DEV: 武器改装面板",
				defaultDesc = "打开模块化武器的改装和调试面板。",
				icon = Dialog_SSRWeaponDebugPanel.PanelIcon,
				action = delegate
				{
					Find.WindowStack.Add(new Dialog_SSRWeaponDebugPanel(this));
				}
			};
		}
	}

	public override void Notify_Equipped(Pawn pawn)
	{
		base.Notify_Equipped(pawn);
		_statCache.MarkDirty();
		_resourcePoolsMightBeDirty = true;
		InvalidateGraphicCache();
	}

	public override void Notify_ColorChanged()
	{
		base.Notify_ColorChanged();
		InvalidateGraphicCache();
	}

	private Graphic GetBaseGraphic_Safe()
	{
		if (_baseGraphic_safe == null)
		{
			if (def.graphicData == null)
			{
				_baseGraphic_safe = BaseContent.BadGraphic;
			}
			else
			{
				_baseGraphic_safe = def.graphicData.Graphic;
			}
		}
		return _baseGraphic_safe.GetColoredVersion(ShaderDatabase.Cutout, DrawColor, DrawColorTwo);
	}

	public void InvalidateGraphicCache()
	{
		_isGraphicDirty = true;
		if (Holder?.Drawer?.renderer != null)
		{
			Holder.Drawer.renderer.SetAllGraphicsDirty();
		}
	}

	public Graphic GetModularGraphic()
	{
		if (def == null)
		{
			return GetBaseGraphic_Safe();
		}
		if (def.GetModExtension<SlottableDefExtension>() == null)
		{
			return GetBaseGraphic_Safe();
		}
		if (_isGraphicDirty || _cachedModularGraphic == null)
		{
			try
			{
				_cachedModularGraphic = GenerateModularGraphic_Internal();
			}
			catch (Exception arg)
			{
				Log.Error($"[SSR_Combat] 生成模块化贴图时发生意外错误 for {def.defName}: {arg}");
				_cachedModularGraphic = GetBaseGraphic_Safe();
			}
			finally
			{
				_isGraphicDirty = false;
			}
		}
		return _cachedModularGraphic ?? GetBaseGraphic_Safe();
	}

	private Graphic GenerateModularGraphic_Internal()
	{
		if (UnifiedExt == null)
		{
			Log.ErrorOnce("[SSR_Combat] 武器 '" + def.defName + "' 缺少 'ModularSlottableDefExtension'。", def.shortHash);
			return GetBaseGraphic_Safe();
		}
		string text = ((!UnifiedExt.canvasTexPath.NullOrEmpty()) ? UnifiedExt.canvasTexPath : def.graphicData?.texPath);
		if (text.NullOrEmpty())
		{
			Log.Error("[SSR_Combat] 武器 '" + def.defName + "' 未定义 'canvasTexPath' 或 'graphicData.texPath'。");
			return GetBaseGraphic_Safe();
		}
		Texture2D texture2D = ContentFinder<Texture2D>.Get(text);
		if (texture2D == null)
		{
			return GetBaseGraphic_Safe();
		}
		RenderTexture temporary = RenderTexture.GetTemporary(texture2D.width, texture2D.height, 24, RenderTextureFormat.ARGB32);
		List<WeaponRenderRequest> list = new List<WeaponRenderRequest>();
		List<WeaponRenderRequest> list2 = new List<WeaponRenderRequest>();
		CollectRenderables(list, list2);
		List<WeaponRenderRequest> list3 = new List<WeaponRenderRequest>(list.Count + list2.Count);
		list3.AddRange(list);
		list3.AddRange(list2);
		WeaponCameraRenderer.Render(temporary, list3, (float)temporary.height / 2f);
		Texture2D texture2D2 = new Texture2D(temporary.width, temporary.height, TextureFormat.ARGB32, mipChain: false);
		RenderTexture.active = temporary;
		texture2D2.ReadPixels(new Rect(0f, 0f, temporary.width, temporary.height), 0, 0);
		texture2D2.Apply();
		RenderTexture.active = null;
		RenderTexture.ReleaseTemporary(temporary);
		Vector2 drawSize = def.graphicData?.drawSize ?? Vector2.one;
		Shader shader = def.graphicData?.shaderType?.Shader ?? ShaderDatabase.Cutout;
		return GraphicDatabase.Get(typeof(Graphic_Single), texture2D2, shader, drawSize, DrawColor, DrawColorTwo, def.graphicData, null);
	}

	private void CollectRenderables(List<WeaponRenderRequest> outlineRequests, List<WeaponRenderRequest> mainRequests)
	{
		if (UnifiedExt?.slots == null)
		{
			return;
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (IAttachable allInstalledAttachment in AllInstalledAttachments)
		{
			if (allInstalledAttachment.def.hidesBodyParts != null)
			{
				hashSet.UnionWith(allInstalledAttachment.def.hidesBodyParts);
			}
		}
		foreach (WeaponSlotData slot in UnifiedExt.slots)
		{
			if (slot.visuals != null && !GetAttachmentsInSlot(slot.slotDef).Any() && !hashSet.Contains(slot.slotDef))
			{
				WeaponSlotData.Visuals visuals = slot.visuals;
				Matrix4x4 transformMatrix = Matrix4x4.Translate(new Vector3(0f, visuals.offset.y, 0f));
				CreateAndAddRequestsForPart(pixelOffset: new Vector2(visuals.offset.x, visuals.offset.z), outlineList: outlineRequests, mainList: mainRequests, texPath: visuals.texturePath, outlinePath: visuals.outlinePath, scale: visuals.scale, transformMatrix: transformMatrix);
			}
		}
		CollectAttachmentRenderablesRecursive(outlineRequests, mainRequests, this, Matrix4x4.identity, isParentFunctionallyDisabled: false);
	}

	private void CollectAttachmentRenderablesRecursive(List<WeaponRenderRequest> outlineList, List<WeaponRenderRequest> mainList, ISlotContainer container, Matrix4x4 parentMatrix, bool isParentFunctionallyDisabled)
	{
		foreach (IAttachable allInstalledAttachment in container.AllInstalledAttachments)
		{
			bool flag = allInstalledAttachment is AttachmentBase attachmentBase && attachmentBase.IsBroken;
			bool flag2 = isParentFunctionallyDisabled || flag;
			AttachmentDef.RenderProperties renderProperties = allInstalledAttachment.def.renderProperties;
			if (!flag2 && renderProperties != null)
			{
				Matrix4x4 matrix4x = parentMatrix;
				Vector3 vector;
				if (allInstalledAttachment is AttachmentBase { useDebugValues: not false } attachmentBase2)
				{
					matrix4x *= Matrix4x4.TRS(attachmentBase2.debugOffset, Quaternion.AngleAxis(attachmentBase2.debugRotation.y, Vector3.up), Vector3.one);
					vector = attachmentBase2.debugScale;
				}
				else
				{
					matrix4x *= Matrix4x4.TRS(renderProperties.renderOffset, Quaternion.identity, Vector3.one);
					vector = renderProperties.renderScale;
				}
				CreateAndAddRequestsForPart(outlineList, mainList, renderProperties.graphicPath, renderProperties.outlinePath, Vector2.zero, new Vector2(vector.x, vector.z), matrix4x);
				if (allInstalledAttachment is ISlotContainer container2)
				{
					bool isParentFunctionallyDisabled2 = flag2 && (allInstalledAttachment.def.failureLogic?.childAttachmentsFailWhenBroken ?? true);
					CollectAttachmentRenderablesRecursive(outlineList, mainList, container2, matrix4x, isParentFunctionallyDisabled2);
				}
			}
		}
	}

	private void CreateAndAddRequestsForPart(List<WeaponRenderRequest> outlineList, List<WeaponRenderRequest> mainList, string texPath, string outlinePath, Vector2 pixelOffset, Vector2 scale, Matrix4x4 transformMatrix)
	{
		if (!outlinePath.NullOrEmpty())
		{
			Matrix4x4 baseMatrix = transformMatrix * Matrix4x4.Translate(new Vector3(0f, -0.01f, 0f));
			outlineList.AddRange(CreateSingleRequest(outlinePath, pixelOffset, scale, baseMatrix));
		}
		if (!texPath.NullOrEmpty())
		{
			mainList.AddRange(CreateSingleRequest(texPath, pixelOffset, scale, transformMatrix));
		}
	}

	private IEnumerable<WeaponRenderRequest> CreateSingleRequest(string texPath, Vector2 pixelOffset, Vector2 scale, Matrix4x4 baseMatrix)
	{
		Texture2D texture2D = ContentFinder<Texture2D>.Get(texPath);
		if (texture2D != null)
		{
			Matrix4x4 matrix4x = Matrix4x4.TRS(new Vector3(pixelOffset.x, 0f, pixelOffset.y), Quaternion.identity, new Vector3((float)texture2D.width * scale.x, 1f, (float)texture2D.height * scale.y));
			if (!materialCache.TryGetValue(texture2D, out var value))
			{
				value = new Material(ShaderDatabase.Transparent)
				{
					mainTexture = texture2D
				};
				materialCache.Add(texture2D, value);
			}
			yield return new WeaponRenderRequest
			{
				Material = value,
				Matrix = baseMatrix * matrix4x
			};
		}
	}
}
