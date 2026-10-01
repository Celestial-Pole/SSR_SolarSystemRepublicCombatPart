using System;
using AK_DLL;
using UnityEngine;
using Verse;

namespace SSR.Combat.Offscreen
{
    //通过核心框架按需加载战斗资源包，与普通炮塔共用资源定义和装载缓存。
    internal static class CombatAssetBundle
    {
        private const string AssetDefName = "SSR_Animate_Prefab_Windows_Combat";

        //确保资源包已经装载，缺少定义或文件时报告具体位置。
        internal static AssetBundle Require()
        {
            var definition = DefDatabase<AssetDef>.GetNamed(AssetDefName, false);
            if (definition == null)
                throw new InvalidOperationException("[SSR离屏] 缺少战斗资源定义：" + AssetDefName);
            var bundle = Utilities_Unity.LoadAssetBundle(definition);
            if (!bundle)
                throw new InvalidOperationException("[SSR离屏] 无法装载战斗资源包：" + definition.modID
                    + " / Asset/" + definition.assetBundle);
            return bundle;
        }
    }
}
