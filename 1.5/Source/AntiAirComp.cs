using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SSRCP_Libs
{
    public class AntiAirComp : ThingComp
    {
        public struct PdOpts : IExposable
        {
            public bool AtPawns;
            public bool AtProjectiles;
            public bool AtPods;

            public void ExposeData()
            {
                Scribe_Values.Look(ref AtPawns, "atPawns", true);
                Scribe_Values.Look(ref AtPods, "atPods", true);
                Scribe_Values.Look(ref AtProjectiles, "atProjectiles", true);
            }
        }

        public PdOpts Opts = new PdOpts()
        {
            AtPawns = true,
            AtPods = true,
            AtProjectiles = true
        };

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref Opts, "pdOpts");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (parent.Faction == Faction.OfPlayer)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "SSRCP_Libs.FireAtPods".Translate(),
                    defaultDesc = "SSRCP_Libs.FireAtPodsDesc".Translate(),
                    //icon = FireAtPodsTex,
                    isActive = () => Opts.AtPods,
                    toggleAction = () => Opts.AtPods = !Opts.AtPods
                };
                yield return new Command_Toggle
                {
                    defaultLabel = "SSRCP_Libs.FireAtPawns".Translate(),
                    defaultDesc = "SSRCP_Libs.FireAtPawnsDesc".Translate(),
                    //icon = FireAtPawnsTex,
                    isActive = () => Opts.AtPawns,
                    toggleAction = () => Opts.AtPawns = !Opts.AtPawns
                };
                yield return new Command_Toggle
                {
                    defaultLabel = "SSRCP_Libs.FireAtProjectiles".Translate(),
                    defaultDesc = "SSRCP_Libs.FireAtProjectilesDesc".Translate(),
                    //icon = FireAtProjectilesTex,
                    isActive = () => Opts.AtProjectiles,
                    toggleAction = () => Opts.AtProjectiles = !Opts.AtProjectiles
                };
            }
        }
    }
}