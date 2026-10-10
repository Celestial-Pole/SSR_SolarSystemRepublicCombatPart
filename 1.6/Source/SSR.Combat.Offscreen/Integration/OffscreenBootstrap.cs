using FS_SSR;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace SSR.Combat.Offscreen
{
    //注册离屏炮塔的模型、射击和原版接口适配。
    [StaticConstructorOnStartup]
    internal static class OffscreenBootstrap
    {
        //安装模型回调及炮塔专用补丁。
        static OffscreenBootstrap()
        {
            var harmony = new Harmony("SSR.Combat.Offscreen");
            harmony.Patch(AccessTools.PropertyGetter(typeof(TC_PrefabDrawer), "CurrentUnityObject"),
                postfix: new HarmonyMethod(typeof(OffscreenBootstrap), nameof(ModelReady)));
            harmony.Patch(AccessTools.Method(typeof(GenDraw), nameof(GenDraw.DrawRadiusRing), new[] { typeof(IntVec3), typeof(float) }),
                prefix: new HarmonyMethod(typeof(TurretRangeRing), nameof(TurretRangeRing.DrawLargeRange)));
            harmony.Patch(AccessTools.Method(typeof(Verb), nameof(Verb.TryFindShootLineFromTo)),
                prefix: new HarmonyMethod(typeof(TurretShootLine), nameof(TurretShootLine.Find)));
            harmony.Patch(AccessTools.Method(typeof(Projectile), "CanHit"),
                prefix: new HarmonyMethod(typeof(TurretProjectileCollision), nameof(TurretProjectileCollision.CanHit)));
            harmony.Patch(AccessTools.Method(typeof(AvoidGrid), "PrintAvoidGridAroundTurret"),
                prefix: new HarmonyMethod(typeof(TurretAvoidGrid), nameof(TurretAvoidGrid.PrintLargeRange)));
        }

        //捕获新模型，其他模组或核心包的普通预制体不受影响。
        private static void ModelReady(TC_PrefabDrawer __instance, TC_PrefabDrawer.UnityGameObjectUpdater __result)
        {
            (__instance as OffscreenTurretComp)?.Observe(__result);
        }
    }
}
