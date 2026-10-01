using FS_SSR;
using HarmonyLib;
using Verse;

namespace SSR.Combat.Offscreen
{
    //接入核心包的模型创建入口，在相机渲染前接管目标炮塔的网格。
    [StaticConstructorOnStartup]
    internal static class OffscreenBootstrap
    {
        //安装限定于离屏组件的模型创建回调。
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
        }

        //捕获新模型，其他模组或核心包的普通预制体不受影响。
        private static void ModelReady(TC_PrefabDrawer __instance, TC_PrefabDrawer.UnityGameObjectUpdater __result)
        {
            (__instance as OffscreenTurretComp)?.Observe(__result);
        }
    }
}
