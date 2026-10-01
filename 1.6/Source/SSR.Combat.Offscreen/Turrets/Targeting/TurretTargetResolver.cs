using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //将原版二维飞行表现和自定义三维目标转换为相同的炮塔目标坐标。
    internal static class TurretTargetResolver
    {
        //沿用原版绘制方法的签名，只读取空投物的实际绘制位置。
        private delegate void SkyfallerPosition(Skyfaller thing, ref Vector3 position, out float rotation);
        private static readonly SkyfallerPosition ReadSkyfaller = (SkyfallerPosition)Delegate.CreateDelegate(
            typeof(SkyfallerPosition), AccessTools.Method(typeof(Skyfaller), "GetDrawPositionAndRotation"));
        private static readonly Func<Projectile, float> ArcHeight = (Func<Projectile, float>)Delegate.CreateDelegate(
            typeof(Func<Projectile, float>), AccessTools.PropertyGetter(typeof(Projectile), "ArcHeightFactor"));
        private static readonly Func<Projectile, float> ArcFraction = (Func<Projectile, float>)Delegate.CreateDelegate(
            typeof(Func<Projectile, float>), AccessTools.PropertyGetter(typeof(Projectile), "DistanceCoveredFractionArc"));

        //优先使用显式高度接口，也允许第三方通过 ThingComp 实现相同接口。
        internal static ITurretSpatialTarget SpatialProvider(Thing thing)
        {
            if (thing is ITurretSpatialTarget provider) return provider;
            if (thing is ThingWithComps withComps)
                foreach (var comp in withComps.AllComps)
                    if (comp is ITurretSpatialTarget component) return component;
            return null;
        }

        //标识需要进入共享空中候选列表的实体。
        internal static bool IsAirTarget(Thing thing)
        {
            return thing is Skyfaller || thing is Projectile projectile && projectile.def.projectile.flyOverhead
                || SpatialProvider(thing) != null;
        }

        //读取目标的当前高度，禁止把原版的 AltitudeLayer 当作飞行高度。
        internal static bool TryRead(LocalTargetInfo target, float groundHeight, out TurretTargetState state)
        {
            state = default;
            if (!target.IsValid) return false;
            Thing thing = target.Thing;
            if (thing != null && (!thing.Spawned || thing.Destroyed)) return false;
            var provider = SpatialProvider(thing);
            if (provider != null) return provider.TryGetTurretTarget(out state);
            float projection = (TurretCaptureProfile.Direction * Vector3.up).y;
            if (thing is Projectile projectile)
            {
                if (!projectile.def.projectile.flyOverhead) return false;
                float height = ArcHeight(projectile) * GenMath.InverseParabola(ArcFraction(projectile)) / projection;
                state = new TurretTargetState(TurretTargetKind.OverheadProjectile, projectile.ExactPosition, height, Vector3.zero);
            }
            else if (thing is Skyfaller skyfaller)
            {
                Vector3 ground = skyfaller.TrueCenter(), draw = skyfaller.DrawPos;
                ReadSkyfaller(skyfaller, ref draw, out _);
                float height = Mathf.Max(0, (draw.z - ground.z) / projection);
                ground.x = draw.x;
                state = new TurretTargetState(TurretTargetKind.Skyfaller, ground, height, Vector3.zero);
            }
            else state = new TurretTargetState(TurretTargetKind.Ground,
                thing != null ? thing.DrawPos : target.CenterVector3, groundHeight, Vector3.zero);
            return true;
        }
    }
}
