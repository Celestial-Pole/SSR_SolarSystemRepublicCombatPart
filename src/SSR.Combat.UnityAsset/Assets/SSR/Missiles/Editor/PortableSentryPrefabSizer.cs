using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SSR.UnityComponent.Outline.Editor
{
    //将便携式哨戒机枪的内部模型缩放到单格范围，并构建对应资源包。
    internal static class PortableSentryPrefabSizer
    {
        private const string PrefabPath = "Assets/SSR/Prefab/TurretT1_STGP_1X.prefab";
        private static string RequestPath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../.local/build-requests/portable-sentry.request"));

        //执行单次资产构建，并将成功尺寸或错误保存到请求文件。
        [MenuItem("SSR/炮塔资源/便携哨戒机枪适配单格")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RequestPath));
            try
            {
                string result = Resize();
                result += "\n" + CombatWindowsBundleBuilder.Build();
                File.WriteAllText(RequestPath, result, new UTF8Encoding(false));
                Debug.Log(result);
            }
            catch (Exception exception)
            {
                File.WriteAllText(RequestPath, exception.ToString(), new UTF8Encoding(false));
                throw;
            }
        }

        //按游戏已有外层比例测量，只在预制体内部保存统一缩放和地面位置。
        private static string Resize()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var root = prefab.transform.Find("Root");
                if (!root) throw new InvalidOperationException("便携式哨戒机枪缺少 Root 节点。");
                //外层比例仅用于测量，保存前恢复原值，游戏逻辑不承担尺寸修正。
                var originalScale = prefab.transform.localScale;
                prefab.transform.localScale = new Vector3(1.5f, 1, 1.5f);
                var before = Measure(prefab);
                float factor = 0.9f / Mathf.Max(before.size.x, before.size.z);
                root.localScale *= factor;
                root.localPosition *= factor;
                var after = Measure(prefab);
                root.position -= new Vector3(after.center.x, after.min.y, after.center.z);
                after = Measure(prefab);
                var savedScale = root.localScale;
                prefab.transform.localScale = originalScale;
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                AssetDatabase.SaveAssets();
                return $"便携式哨戒机枪：原尺寸={before.size}；单格尺寸={after.size}；Root 缩放={savedScale}";
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        //仅合并有效且启用的实体网格，排除旧描边外壳和停用部件。
        private static Bounds Measure(GameObject prefab)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.enabled && !renderer.sharedMaterials.Any(material =>
                    material && material.shader && material.shader.name.Contains("Wireframe"))).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("便携式哨戒机枪没有可测量的网格。");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
