using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace FS_TransWeapon;

public class MultiformWeaponDef : ThingDef
{
	public List<ThingDef> weaponMode;

	public override void ResolveReferences()
	{
		base.ResolveReferences();
		List<VerbProperties> list = new List<VerbProperties>();
		foreach (ThingDef item in weaponMode)
		{
			list.Add(item.Verbs[0]);
		}
		FieldInfo field = typeof(ThingDef).GetField("verbs", BindingFlags.Instance | BindingFlags.NonPublic);
		field.SetValue(this, list);
	}
}
