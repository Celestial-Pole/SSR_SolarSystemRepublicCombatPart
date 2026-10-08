using System;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //为电磁炮火控雷达建立与炮管分离的俯仰轴。
    internal static class RailgunRadarBuilder
    {
        //整理三款电磁炮雷达，盘面正向统一为节点局部 Z 轴。
        [MenuItem("SSR/炮塔资源/整理电磁炮雷达俯仰")]
        public static void Build()
        {
            BuildOne("TurretT1_3X", false);
            BuildOne("TurretT1_5X", false);
            BuildOne("Turret500mmPaletteGame", true);
            AssetDatabase.SaveAssets();
        }

        //保留雷达转轴位置，将盘面与背板从固定支架中分离。
        private static void BuildOne(string name, bool heavy)
        {
            string path = "Assets/SSR/Prefab/" + name + ".prefab";
            var model = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var root = model.transform.Find("Transform_Y/Root");
                var yaw = root.Find(heavy ? "Yaw" : "Transform_X/Transform_Z/TurretTop");
                if (yaw.Find("RadarPitch")) throw new InvalidOperationException(name + " 已有雷达俯仰轴。");
                var disk = yaw.Find(heavy ? "柱体.003/模型_柱体.003" : "模型_柱体.007").GetComponent<MeshFilter>();
                var muzzle = yaw.Find(heavy ? "Pitch/Recoil/FirePoint" : "MainGun/FirePoint");
                Vector3 forward = muzzle.TransformVector(heavy ? Vector3.forward : Vector3.right).normalized;
                var pivot = new GameObject("RadarPitch").transform;
                pivot.SetParent(yaw, false);
                pivot.SetPositionAndRotation(disk.GetComponent<Renderer>().bounds.center,
                    Quaternion.LookRotation(FrontNormal(disk, forward), root.up));
                string[] parts = heavy ? new[] { "柱体.003", "柱体.005", "立方体.017" }
                    : new[] { "模型_柱体.007", "模型_立方体.007", "模型_立方体.008" };
                foreach (string part in parts) yaw.Find(part).SetParent(pivot, true);
                pivot.rotation = Quaternion.LookRotation(forward, root.up);
                PrefabUtility.SaveAsPrefabAsset(model, path, out bool saved);
                if (!saved) throw new InvalidOperationException(name + " 雷达预制体保存失败。");
                Debug.Log(name + "：火控雷达已建立独立俯仰轴并对齐炮口。");
            }
            finally { PrefabUtility.UnloadPrefabContents(model); }
        }

        //取朝向炮口一侧的最大三角面法线作为雷达盘面正向。
        private static Vector3 FrontNormal(MeshFilter disk, Vector3 forward)
        {
            var vertices = disk.sharedMesh.vertices;
            var triangles = disk.sharedMesh.triangles;
            Vector3 largest = Vector3.zero;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 first = disk.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 area = Vector3.Cross(disk.transform.TransformPoint(vertices[triangles[i + 1]]) - first,
                    disk.transform.TransformPoint(vertices[triangles[i + 2]]) - first);
                if (Vector3.Dot(area, forward) > 0 && area.sqrMagnitude > largest.sqrMagnitude) largest = area;
            }
            if (largest.sqrMagnitude == 0) throw new InvalidOperationException("无法确定雷达盘面正向：" + disk.name);
            return largest.normalized;
        }
    }
}
