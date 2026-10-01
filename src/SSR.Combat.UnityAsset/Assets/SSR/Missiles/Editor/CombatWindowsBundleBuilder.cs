using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.UnityComponent.Outline.Editor
{
    //按现有资源标签构建单个 Windows 战斗资源包，不改动其他平台或用户场景。
    internal static class CombatWindowsBundleBuilder
    {
        //编译明确登记的资源及 Shader，并将构建错误交给调用者。
        internal static string Build()
        {
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
