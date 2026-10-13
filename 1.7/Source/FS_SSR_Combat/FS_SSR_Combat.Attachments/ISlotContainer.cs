using System.Collections.Generic;
using Verse;

namespace FS_SSR_Combat.Attachments;

public interface ISlotContainer
{
	IEnumerable<SlotPropertyDef> AllSlotProperties { get; }

	IEnumerable<IAttachable> AllInstalledAttachments { get; }

	AttachmentStatCache StatCache { get; }

	Thing RootHost { get; }

	Pawn Holder { get; }

	bool TryGetSlotProperty(string slotId, out SlotPropertyDef slotProperty);

	IEnumerable<IAttachable> GetAttachmentsInSlot(string slotId);

	bool CanInstall(string slotId, IAttachable item);

	bool TryInstall(string slotId, IAttachable item);

	bool TryUninstall(IAttachable item);

	void RegisterTickable(IAttachable tickable);

	void DeregisterTickable(IAttachable tickable);

	void Notify(IAttachable source, AttachmentNotificationType type);
}
