using System;
using Verse;

namespace FS_SSR_Combat.Attachments;

public static class AttachmentFactory
{
	public static IAttachable MakeAttachment(AttachmentDef def)
	{
		if (def == null)
		{
			Log.Error("[CAF] Cannot create an attachment from a null AttachmentDef.");
			return null;
		}
		try
		{
			IAttachable attachable = (IAttachable)Activator.CreateInstance(def.attachmentClass);
			if (attachable is AttachmentBase attachmentBase)
			{
				attachmentBase.def = def;
			}
			else
			{
				Log.Error("[CAF] Attachment class " + def.attachmentClass.FullName + " does not inherit from AttachmentBase. Cannot assign def.");
			}
			return attachable;
		}
		catch (Exception ex)
		{
			Log.Error("[CAF] Failed to create instance of " + def.attachmentClass.FullName + " for def " + def.defName + ". Exception: " + ex.Message);
			return null;
		}
	}
}
