using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //缓存制导弹体和烟焰资源。
    internal sealed class CombinedDefenseAssets
    {
        private static readonly Dictionary<GuidedMissileSettings, CombinedDefenseAssets> cache
            = new Dictionary<GuidedMissileSettings, CombinedDefenseAssets>();
        internal readonly GameObject Missile, Flame, Smoke;
        internal readonly Vector3 Tip, Trail;

        //加载弹体资源并校验弹头、尾喷口挂点。
        private CombinedDefenseAssets(GuidedMissileSettings settings)
        {
            Missile = SsrModelLibrary.Require(settings.missilePrefabPath);
            Flame = SsrModelLibrary.Require(settings.exhaust.flamePath);
            Smoke = SsrModelLibrary.Require(settings.exhaust.smokePath);
            var tip = Missile.transform.FindModelNode("TipPoint");
            var trail = Missile.transform.FindModelNode("TrailPoint");
            if (!tip || !trail) throw new InvalidOperationException("制导弹体缺少 TipPoint 或 TrailPoint：" + settings.missilePrefabPath);
            Tip = Missile.transform.InverseTransformPoint(tip.position);
            Trail = Missile.transform.InverseTransformPoint(trail.position);
        }

        //按配置缓存资源。
        internal static CombinedDefenseAssets Get(GuidedMissileSettings settings)
        {
            if (!cache.TryGetValue(settings, out var assets)) cache.Add(settings, assets = new CombinedDefenseAssets(settings));
            return assets;
        }

    }
}
