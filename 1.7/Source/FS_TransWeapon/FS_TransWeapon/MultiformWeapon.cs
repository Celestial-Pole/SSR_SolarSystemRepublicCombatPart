using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace FS_TransWeapon;

public class MultiformWeapon : ThingWithComps
{
	private CompEquippable compEq;

	public int currentWeapon;

	public List<CachedWeaponStat> cachedWeapons;

	private List<Verb> cachedAllVerbs;

	private MultiformWeaponDef Def => def as MultiformWeaponDef;

	private CompEquippable CompEq
	{
		get
		{
			if (compEq == null)
			{
				compEq = GetComp<CompEquippable>();
			}
			return compEq;
		}
	}

	public Verb ActualVerb => CompEq.AllVerbs[0];

	public MultiformWeaponDef OriginDef => def as MultiformWeaponDef;

	public List<ThingDef> WeaponMode => OriginDef.weaponMode;

	public override Graphic Graphic => ActualDef.graphicData.GraphicColoredFor(this);

	public ThingDef ActualDef => FormDefAtIndex(currentWeapon);

	public ThingDef FormDefAtIndex(int i)
	{
		if (i >= WeaponMode.Count)
		{
			Log.Error("FS. multiform weapon out of array");
			i = 0;
		}
		return WeaponMode[i];
	}

	public override void Notify_Equipped(Pawn pawn)
	{
		base.Notify_Equipped(pawn);
	}

	public override void Notify_Unequipped(Pawn pawn)
	{
		base.Notify_Unequipped(pawn);
	}

	public override void PostPostMake()
	{
		base.PostPostMake();
		InitAllVerbs();
		ChangeWeaponTo(0);
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);
		Initialize();
		ChangeWeaponTo(currentWeapon);
	}

	public override void ExposeData()
	{
		if (Scribe.mode == LoadSaveMode.Saving)
		{
			FieldInfo field = typeof(VerbTracker).GetField("verbs", BindingFlags.Instance | BindingFlags.NonPublic);
			field.SetValue(CompEq.verbTracker, cachedAllVerbs.ToList());
		}
		Scribe_Values.Look(ref currentWeapon, "current", 0);
		base.ExposeData();
		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			Initialize();
			InitAllVerbs();
			ChangeWeaponTo(currentWeapon);
		}
	}

	public void ChangeWeaponTo(int i)
	{
		if (i >= WeaponMode.Count)
		{
			Log.Error("FS. multiform weapon out of array");
			i = 0;
		}
		Log.Message($"change weapon to {i}");
		currentWeapon = i;
		FieldInfo field = typeof(VerbTracker).GetField("verbs", BindingFlags.Instance | BindingFlags.NonPublic);
		Thing caster = CompEq.verbTracker.AllVerbs[0].caster;
		cachedAllVerbs[i].caster = caster;
		field.SetValue(CompEq.verbTracker, new List<Verb> { cachedAllVerbs[i] });
		if (base.Map != null)
		{
			DirtyMapMesh(base.Map);
		}
	}

	public void Initialize()
	{
		if (cachedWeapons != null)
		{
			return;
		}
		if (WeaponMode == null || WeaponMode.Count == 0)
		{
			Log.Error("[FS] " + def.defName + " lacks any transform candidate");
			return;
		}
		cachedWeapons = new List<CachedWeaponStat>();
		foreach (ThingDef item in WeaponMode)
		{
			cachedWeapons.Add(new CachedWeaponStat(item));
		}
	}

	public void InitAllVerbs()
	{
		if (cachedAllVerbs == null)
		{
			cachedAllVerbs = CompEq.AllVerbs.ToList();
		}
	}
}
