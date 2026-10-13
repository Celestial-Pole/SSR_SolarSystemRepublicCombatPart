using RimWorld;

namespace FS_SSR_Combat.Attachments;

[DefOf]
public static class AttachmentStatDefOf
{
	public static AttachmentStatDef MaxHP;

	public static AttachmentStatDef ResourceCapacity;

	static AttachmentStatDefOf()
	{
		DefOfHelper.EnsureInitializedInCtor(typeof(AttachmentStatDefOf));
	}
}
