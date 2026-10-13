using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments.ModExtension;

public class WeaponSlotData
{
	public class Visuals
	{
		[NoTranslate]
		public string texturePath;

		[NoTranslate]
		public string outlinePath;

		public Vector3 offset = Vector3.zero;

		public Vector2 scale = Vector2.one;
	}

	[NoTranslate]
	public string slotDef;

	public Visuals visuals;
}
