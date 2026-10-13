using System;
using System.Collections.Generic;
using SSR.UnityComponent.Missiles;
using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //装载资源构建生成的布局，供无显示模型的地图逻辑和离屏显示共用。
    internal sealed class SiloAssets
    {
        private static readonly Dictionary<SiloSettings, SiloAssets> cache = new Dictionary<SiloSettings, SiloAssets>();
        internal readonly GameObject Prefab, Missile, Flame, Smoke;
        internal readonly AnimationClip[] Openings = new AnimationClip[9];
        internal readonly SiloLayoutData Layout;

        //验证必需的资源和时序参数，配置错误直接报告。
        private SiloAssets(SiloSettings settings)
        {
            var bundle = CombatAssetBundle.Require();
            Prefab = Require<GameObject>(bundle, settings.prefabPath);
            Missile = Require<GameObject>(bundle, settings.missilePath);
            Flame = Require<GameObject>(bundle, settings.flamePath);
            Smoke = Require<GameObject>(bundle, settings.smokePath);
            Layout = SiloLayoutReader.Read(Require<TextAsset>(bundle, settings.layoutPath).text, settings.layoutPath);
            if (!Prefab.transform.Find(Layout.animatorPath))
                throw new InvalidOperationException("导弹井动画根节点不存在：" + Layout.animatorPath);
            for (int i = 0; i < Layout.slots.Length; i++)
            {
                var slot = Layout.slots[i];
                if (!Prefab.transform.Find(slot.path))
                    throw new InvalidOperationException("导弹井库存节点不存在：" + slot.path);
                Openings[i] = Require<AnimationClip>(bundle, slot.openingPath);
                if (Mathf.Abs(slot.openingSeconds - Openings[i].length) > 0.001f)
                    throw new InvalidOperationException("舱盖布局时长与独立动画不一致：" + slot.path);
            }
            SiloSettingsValidator.Validate(settings, Layout);
        }

        //按建筑配置缓存只读资源，不持有任何地图实例。
        internal static SiloAssets Get(SiloSettings settings)
        {
            if (settings == null) throw new InvalidOperationException("导弹井建筑缺少 SiloSettings 配置。");
            if (!cache.TryGetValue(settings, out var assets)) cache.Add(settings, assets = new SiloAssets(settings));
            return assets;
        }

        //读取指定类型的必需资源，路径错误立即终止装载。
        private static T Require<T>(AssetBundle bundle, string path) where T : UnityEngine.Object
        {
            var asset = bundle.LoadAsset<T>(path);
            if (!asset) throw new InvalidOperationException("导弹井缺少资源：" + path);
            return asset;
        }
    }
}
