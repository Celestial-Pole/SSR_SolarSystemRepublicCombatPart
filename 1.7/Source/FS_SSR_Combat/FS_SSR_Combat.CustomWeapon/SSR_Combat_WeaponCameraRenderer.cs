using Verse;

namespace FS_SSR_Combat.CustomWeapon;

[StaticConstructorOnStartup]
public static class SSR_Combat_WeaponCameraRenderer
{
	static SSR_Combat_WeaponCameraRenderer()
	{
		WeaponCameraRenderer.Initialize();
	}
}
