using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using Verse.Noise;
using Verse.Grammar;
using RimWorld;
using RimWorld.Planet;

// using System.Reflection;
// using HarmonyLib;

namespace SSRCP_Libs
{
    [StaticConstructorOnStartup]
    public static class SSRCP_Libs
    {
        static SSRCP_Libs()
        {
            var harm = new Harmony("SSRCP_Libs");
            AntiAirPatch.ApplyPatch(harm);
        }
    }
}
