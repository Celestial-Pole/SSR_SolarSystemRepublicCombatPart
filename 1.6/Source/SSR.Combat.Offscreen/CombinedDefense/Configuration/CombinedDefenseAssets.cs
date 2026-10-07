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
            var bundle = CombatAssetBundle.Require();
            Missile = Require(bundle, settings.missilePrefabPath);
            Flame = Require(bundle, settings.exhaust.flamePath);
            Smoke = Require(bundle, settings.exhaust.smokePath);
            var tip = Missile.transform.Find("TipPoint");
            var trail = Missile.transform.Find("TrailPoint");
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

        //加载预制体，缺失时报告路径。
        private static GameObject Require(AssetBundle bundle, string path)
        {
            var result = bundle.LoadAsset<GameObject>(path);
            if (!result) throw new InvalidOperationException("实体导弹缺少资源：" + path);
            return result;
        }
    }
}
