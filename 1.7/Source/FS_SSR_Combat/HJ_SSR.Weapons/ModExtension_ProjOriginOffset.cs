using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HJ_SSR.Weapons;

public class ModExtension_ProjOriginOffset : DefModExtension
{
	public List<Vector2> offsets = new List<Vector2>();

	public Vector2 GetOffsetFor(int index)
	{
		if (offsets.NullOrEmpty())
		{
			return Vector2.zero;
		}
		int index2 = index % offsets.Count;
		return offsets[index2];
	}
}
