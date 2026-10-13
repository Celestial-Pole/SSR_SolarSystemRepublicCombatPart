using RimWorld;
using UnityEngine;

namespace FS_SSR_Combat.Attachments;

public class ApparelGraphicProperties
{
	public BodyTypeDef bodyType;

	public Vector3 wornGraphicOffset = Vector3.zero;

	public Vector3 wornGraphicScale = Vector3.one;

	public Vector3 wornGraphicRotation = Vector3.zero;

	public WornGraphicDirectionalOffsets wornGraphicDirectionalOffsets;
}
