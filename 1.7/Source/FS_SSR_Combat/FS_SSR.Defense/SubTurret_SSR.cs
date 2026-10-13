namespace FS_SSR.Defense;

public class SubTurret_SSR : Turret_SSR
{
	public Turret_SSR parent;

	public override TC_PrefabDrawer PrefabDrawer => parent?.PrefabDrawer;

	protected override void Tick()
	{
		if (parent == null || !parent.Spawned)
		{
			Destroy();
		}
		else
		{
			base.Tick();
		}
	}
}
