using Verse;

namespace FS_SSR_Combat;

public class TC_AbsoluteDamageAddon : ThingComp
{
	public TCP_AbsoluteDamageAddon Props => props as TCP_AbsoluteDamageAddon;

	public DamageDef DamageType => Props.damageType;

	public bool Researched
	{
		get
		{
			if (Props.researchReq == null)
			{
				return true;
			}
			return Props.researchReq.IsFinished;
		}
	}
}
