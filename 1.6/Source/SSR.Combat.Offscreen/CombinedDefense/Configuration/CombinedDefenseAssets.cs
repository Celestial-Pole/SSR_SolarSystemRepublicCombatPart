using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //缓存制导弹体和导弹井共用的烟焰资源，校验弹头与尾喷口挂点。
    internal sealed class CombinedDefenseAssets
    {
        private static readonly Dictionary<CombinedAirDefenseSettings, CombinedDefenseAssets> cache
            = new Dictionary<CombinedAirDefenseSettings, CombinedDefenseAssets>();
        internal readonly GameObject Missile, Flame, Smoke;
        internal readonly Vector3 Tip, Trail;

        //读取完整弹体及真实喷口，不从筒口或模型中心猜测尾迹起点。
        private CombinedDefenseAssets(CombinedAirDefenseSettings settings)
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

        //按只读定义复用资源，不保存地图或预制体实例。
        internal static CombinedDefenseAssets Get(CombinedAirDefenseSettings settings)
        {
            if (!cache.TryGetValue(settings, out var assets)) cache.Add(settings, assets = new CombinedDefenseAssets(settings));
            return assets;
        }

        //装载指定预制体，资源错误直接报告路径。
        private static GameObject Require(AssetBundle bundle, string path)
        {
            var result = bundle.LoadAsset<GameObject>(path);
            if (!result) throw new InvalidOperationException("弹炮合一系统缺少资源：" + path);
            return result;
        }
    }
}
