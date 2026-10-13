using System.Collections.Generic;
using Verse;

namespace FS_SSR_Combat.Attachments.ModExtension;

public class SlottableDefExtension : DefModExtension
{
	[NoTranslate]
	public string canvasTexPath;

	public List<WeaponSlotData> slots;
}
