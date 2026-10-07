using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.UnityComponent.Outline.Editor
{
    //按资源标签构建 Windows 战斗资源包。
    internal static class CombatWindowsBundleBuilder
    {
        //收集运行资源并构建资源包。
        internal static string Build()
        {
            //底色材质不读取法线，但运行时的几何采集和光照需要完整法线数据。
            if (PlayerSettings.stripUnusedMeshComponents)
                throw new InvalidOperationException("战斗资源不能剔除网格法线，请关闭 Player Settings 的 Optimize Mesh Data。");
            const string bundle = "ssr_combat_windows";
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Asset/Windows"));
            //父目录的标签会被脚本继承，构建清单只接收运行资源。
            string[] paths = AssetDatabase.GetAssetPathsFromAssetBundle(bundle)
                .Where(p => !p.Contains("/Editor/") && !p.EndsWith(".cs") && !p.EndsWith(".asmdef")
                    && !p.EndsWith(".asmref") && !p.EndsWith(".cginc") && !p.EndsWith(".hlsl")
                    && !AssetDatabase.IsValidFolder(p)).ToArray();
            if (paths.Length == 0) throw new InvalidOperationException("Windows 战斗资源包没有登记资源。");
            Directory.CreateDirectory(output);
            var manifest = BuildPipeline.BuildAssetBundles(output,
                new[] { new AssetBundleBuild { assetBundleName = bundle, assetNames = paths } },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
            if (!manifest) throw new InvalidOperationException("Windows 战斗资源包构建失败。");
            return "Windows 资源包构建完成，登记资源数=" + paths.Length;
        }
    }
}
