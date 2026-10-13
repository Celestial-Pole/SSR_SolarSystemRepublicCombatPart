using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FS_SSR_Combat.Attachments;

public interface IAttachable : IExposable
{
	AttachmentDef def { get; }

	ISlotContainer Parent { get; }

	string Label { get; }

	string LabelCap { get; }

	string SlotId { get; }

	void OnInstalled(ISlotContainer parent, string slotId);

	void OnUninstalled();

	void Notify_HostLoadedAndLinked(ISlotContainer parent, string slotId);

	void CollectSelfStats(List<StatModifier> outOffsets, List<StatModifier> outFactors, bool isFunctionallyDisabled = false);

	void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed);

	void PostApplyDamage(DamageInfo dinfo, float damageDealt);
}
