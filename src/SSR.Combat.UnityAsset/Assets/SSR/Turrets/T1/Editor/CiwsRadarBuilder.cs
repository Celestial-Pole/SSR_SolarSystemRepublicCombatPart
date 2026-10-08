using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //为电磁近防炮的雷达盘面建立俯仰轴。
    internal static class CiwsRadarBuilder
    {
        //以盘面后端连接点为轴心，保留固定支架。
        [MenuItem("SSR/炮塔资源/整理电磁近防炮雷达俯仰")]
        public static void Build()
        {
            const string path = "Assets/SSR/Prefab/TurretT1_ECIWS_2X.prefab";
            var model = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var root = model.transform.Find("Transform_Y/Root");
                var yaw = root.Find("Transform_X/Transform_Z/TurretTop");
                if (yaw.Find("RadarPitch")) throw new InvalidOperationException("电磁近防炮已有雷达俯仰轴。");
                var disk = yaw.Find("锥体/模型_锥体").GetComponent<MeshFilter>();
                Vector3 forward = yaw.Find("MainGun/FirePoint").TransformVector(Vector3.right).normalized;
                Vector3 normal = DiskNormal(disk, forward);
                var points = disk.sharedMesh.vertices.Select(disk.transform.TransformPoint).ToArray();
                float back = points.Min(point => Vector3.Dot(point, normal));
                var attachment = points.Where(point => Vector3.Dot(point, normal) <= back + 0.00001f).ToArray();
                Vector3 center = attachment.Aggregate(Vector3.zero, (sum, point) => sum + point) / attachment.Length;
                var pivot = new GameObject("RadarPitch").transform;
                pivot.SetParent(yaw, false);
                pivot.SetPositionAndRotation(center, Quaternion.LookRotation(normal, root.up));
                disk.transform.parent.SetParent(pivot, true);
                pivot.rotation = Quaternion.LookRotation(forward, root.up);
                PrefabUtility.SaveAsPrefabAsset(model, path, out bool saved);
                if (!saved) throw new InvalidOperationException("电磁近防炮雷达预制体保存失败。");
                AssetDatabase.SaveAssets();
                Debug.Log("电磁近防炮：雷达盘面已接入独立俯仰轴，支架保持固定。");
            }
            finally { PrefabUtility.UnloadPrefabContents(model); }
        }

        //以最大平面确定盘面轴向，避免将背部锥面的斜面当作正向。
        private static Vector3 DiskNormal(MeshFilter disk, Vector3 forward)
        {
            var vertices = disk.sharedMesh.vertices;
            var triangles = disk.sharedMesh.triangles;
            Vector3 largest = Vector3.zero;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 first = disk.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 area = Vector3.Cross(disk.transform.TransformPoint(vertices[triangles[i + 1]]) - first,
                    disk.transform.TransformPoint(vertices[triangles[i + 2]]) - first);
                if (area.sqrMagnitude > largest.sqrMagnitude) largest = area;
            }
            if (largest.sqrMagnitude == 0) throw new InvalidOperationException("无法确定近防炮雷达盘面轴向。");
            return Vector3.Dot(largest, forward) < 0 ? -largest.normalized : largest.normalized;
        }
    }
}
