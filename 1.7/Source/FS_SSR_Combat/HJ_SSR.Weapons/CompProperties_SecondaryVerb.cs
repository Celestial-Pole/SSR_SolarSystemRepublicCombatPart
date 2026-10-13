using Verse;

namespace HJ_SSR.Weapons;

public class CompProperties_SecondaryVerb : CompProperties
{
	public VerbProperties verbProps = new VerbProperties();

	public string mainCommandIcon = "UI/Icons/FireModes/BDP_Overcharge_Off";

	public string mainWeaponLabel = "BDP_MainWeaponLabelVanilla";

	public string secondaryCommandIcon = "UI/Icons/FireModes/BDP_Overcharge_On";

	public string secondaryWeaponLabel = "BDP_SecondaryWeaponLabelVanilla";

	public string description = "BDP_WeaponDescVanilla";

	public CompProperties_SecondaryVerb()
	{
		compClass = typeof(CompSecondaryVerb);
	}
}
