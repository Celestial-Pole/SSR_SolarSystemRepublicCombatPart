using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //按 MGAA 发射筒内部空间制作带弹头、弹身、尾翼和喷口的完整导弹资源。
    internal static class DefenseMissileMeshBuilder
    {
        internal const string Folder = "Assets/SSR/CombinedDefense/Game";
        internal const string PrefabPath = Folder + "/MGAAMissile.prefab";
        private const int Segments = 32;

        //构建长零点八二、最大直径零点零六八的弹体，并建立真实弹头和喷口挂点。
        internal static GameObject Build()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            var profile = new Vector2[]
            {
                new Vector2(0.011f, -0.370f), new Vector2(0.011f, -0.410f),
                new Vector2(0.027f, -0.410f), new Vector2(0.028f, -0.355f),
                new Vector2(0.029f, -0.350f), new Vector2(0.029f, -0.330f),
                new Vector2(0.028f, -0.325f), new Vector2(0.028f, 0.160f),
                new Vector2(0.029f, 0.170f), new Vector2(0.029f, 0.190f),
                new Vector2(0.028f, 0.200f), new Vector2(0.027f, 0.240f),
                new Vector2(0.022f, 0.310f), new Vector2(0.012f, 0.375f), new Vector2(0, 0.410f)
            };
            for (int ring = 0; ring < profile.Length; ring++)
                for (int side = 0; side < Segments; side++)
                {
                    float angle = side * Mathf.PI * 2 / Segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[ring].x,
                        Mathf.Sin(angle) * profile[ring].x, profile[ring].y));
                }
            for (int ring = 0; ring < profile.Length - 1; ring++)
            {
                int material = ring < 2 ? 0 : ring >= 10 ? 1 : ring == 4 || ring == 8 ? 3 : 2;
                for (int side = 0; side < Segments; side++)
                {
                    int next = (side + 1) % Segments;
                    int a = ring * Segments + side, b = ring * Segments + next;
                    int c = a + Segments, d = b + Segments;
                    triangles[material].AddRange(new[] { a, b, c, b, d, c });
                }
            }
            for (int fin = 0; fin < 4; fin++) AddFin(vertices, triangles[0], fin * 90);
            var mesh = new Mesh { name = "MGAA完整筒射导弹" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = Folder + "/MGAAMissile.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved) { EditorUtility.CopySerialized(mesh, saved); UnityEngine.Object.DestroyImmediate(mesh); mesh = saved; }
            else AssetDatabase.CreateAsset(mesh, meshPath);
            var model = new GameObject("MGAAMissile");
            try
            {
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                model.AddComponent<MeshRenderer>().sharedMaterials = new[]
                { Material("4E5153"), Material("44588A"), Material("6E6F70"), Material("979899") };
                Point(model.transform, "TipPoint", new Vector3(0, 0, 0.410f));
                Point(model.transform, "TrailPoint", new Vector3(0, 0, -0.410f));
                var prefab = PrefabUtility.SaveAsPrefabAsset(model, PrefabPath);
                if (!prefab) throw new InvalidOperationException("MGAA 完整导弹预制体保存失败。");
                AssetImporter.GetAtPath(PrefabPath).SetAssetBundleNameAndVariant("ssr_combat_windows", "");
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        //建立小幅外伸的四片实体尾翼，完整外径始终小于发射筒内径。
        private static void AddFin(List<Vector3> vertices, List<int> triangles, float angle)
        {
            int start = vertices.Count;
            var rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            var contour = new[] { new Vector3(0.025f, -0.001f, -0.360f), new Vector3(0.034f, -0.001f, -0.340f),
                new Vector3(0.034f, -0.001f, -0.265f), new Vector3(0.025f, -0.001f, -0.210f) };
            foreach (var vertex in contour) vertices.Add(rotation * vertex);
            foreach (var vertex in contour) vertices.Add(rotation * (vertex + new Vector3(0, 0.002f, 0)));
            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2,
                start + 4, start + 5, start + 6, start + 4, start + 6, start + 7 });
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                triangles.AddRange(new[] { start + i, start + next, start + i + 4,
                    start + next, start + next + 4, start + i + 4 });
            }
        }

        //使用现有炮塔调色板材质，使装筒和飞行的弹体颜色一致。
        private static Material Material(string color)
        {
            string path = "Assets/SSR/Turrets/T1/Materials/T1_" + color + "FF.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) throw new InvalidOperationException("缺少导弹材质：" + path);
            return material;
        }

        //建立稳定的模型局部挂点，不通过渲染边界猜测喷口。
        private static void Point(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = position;
        }
    }
}
