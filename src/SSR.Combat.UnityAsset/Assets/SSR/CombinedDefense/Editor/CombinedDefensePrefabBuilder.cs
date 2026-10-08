using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //构建弹炮合一的机械层级和筒内导弹。
    internal static class CombinedDefensePrefabBuilder
    {
        private const string Path = "Assets/SSR/Prefab/TurretT1_MGAA_2X.prefab";

        //构建导弹资源和弹炮合一预制体。
        [MenuItem("SSR/炮塔资源/整理弹炮合一炮塔")]
        internal static string Build()
        {
            EnsureFolder("Assets/SSR", "CombinedDefense");
            EnsureFolder("Assets/SSR/CombinedDefense", "Game");
            var missile = DefenseMissileMeshBuilder.Build();
            var prefab = PrefabUtility.LoadPrefabContents(Path);
            try
            {
                if (prefab.transform.Find("Root")) throw new InvalidOperationException("MGAA 已存在机械层级，禁止重复分组。");
                prefab.transform.localScale = Vector3.one * 3;
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true).ToArray();
                var old = prefab.transform.GetChild(0);
                var root = Group(prefab.transform, prefab.transform, "Root", Vector3.zero);
                var yaw = Group(root, prefab.transform, "Yaw", new Vector3(0, 0.155f, 0.003f));
                var pitch = Group(yaw, prefab.transform, "Pitch", new Vector3(0, 0.31137f, 0.00573f));
                var rotor = Group(pitch, prefab.transform, "Rotor", new Vector3(0.000014f, 0.31137f, -0.535f));
                var missileRack = Group(yaw, prefab.transform, "MissileRack", new Vector3(0, 0.326f, 0.405f));
                var left = Group(missileRack, prefab.transform, "RackLeft", new Vector3(-0.394f, 0.326f, 0));
                var right = Group(missileRack, prefab.transform, "RackRight", new Vector3(0.395f, 0.326f, 0));
                var sensorLeft = Group(yaw, prefab.transform, "SensorLeft", new Vector3(-0.169f, 0.397f, 0.168f));
                var sensorRight = Group(yaw, prefab.transform, "SensorRight", new Vector3(0.173f, 0.397f, 0.132f));
                var radar = Group(yaw, prefab.transform, "RadarRear", new Vector3(0, 0.42808f, 0.38262f));
                var rotorNames = new HashSet<string> { "柱体.010", "Slice.007", "柱体.004", "柱体.006", "柱体.007" };
                var sensorLeftNames = new HashSet<string> { "柱体.054", "柱体.056", "立方体.024", "立方体.025" };
                var sensorRightNames = new HashSet<string> { "柱体.050", "柱体.052", "立方体.012", "立方体.023" };
                var radarNames = new HashSet<string> { "立方体.006", "立方体.007", "立方体.008", "立方体.009", "立方体.010" };
                var gunNames = new HashSet<string> { "柱体.009", "立方体.015", "立方体.018" };
                foreach (var filter in meshes)
                {
                    var bounds = LocalBounds(prefab.transform, filter);
                    if (filter.name == "球体" || filter.name.StartsWith("球体."))
                    { UnityEngine.Object.DestroyImmediate(filter.gameObject); continue; }
                    Transform parent = bounds.center.y < 0.16f ? root : yaw;
                    if (rotorNames.Contains(filter.name)) parent = rotor;
                    else if (sensorLeftNames.Contains(filter.name)) parent = sensorLeft;
                    else if (sensorRightNames.Contains(filter.name)) parent = sensorRight;
                    else if (radarNames.Contains(filter.name)) parent = radar;
                    else if (gunNames.Contains(filter.name)) parent = pitch;
                    else if (filter.name == "立方体.014") parent = missileRack;
                    else if (bounds.center.x < -0.30f && bounds.center.y > 0.17f) parent = left;
                    else if (bounds.center.x > 0.30f && bounds.center.y > 0.17f) parent = right;
                    filter.transform.SetParent(parent, true);
                }
                FirePoint(pitch, prefab.transform, "FirePoint", new Vector3(0.000014f, 0.33907f, -0.84120f));
                FirePoint(missileRack, prefab.transform, "AimPoint", new Vector3(0, 0.326f, -0.510f));
                var positions = new[] {
                    new Vector3(-0.350f,0.380f,-0.510f), new Vector3(0.351f,0.380f,-0.510f),
                    new Vector3(-0.438f,0.380f,-0.510f), new Vector3(0.439f,0.380f,-0.510f),
                    new Vector3(-0.350f,0.272f,-0.510f), new Vector3(0.351f,0.272f,-0.510f),
                    new Vector3(-0.438f,0.272f,-0.510f), new Vector3(0.439f,0.272f,-0.510f) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var rack = i % 2 == 0 ? left : right;
                    var slot = Group(rack, prefab.transform, "Slot" + (i + 1).ToString("D2"), positions[i]);
                    FirePoint(slot, prefab.transform, "FirePoint", positions[i]);
                    var payload = (GameObject)PrefabUtility.InstantiatePrefab(missile, prefab.scene);
                    payload.name = "Missile";
                    payload.transform.SetParent(slot, false);
                    payload.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    payload.transform.localPosition = new Vector3(0, 0, 0.410f);
                }
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                //三格模型尺寸保存在根节点，外层缩放由建筑配置控制。
                prefab.transform.localScale = Vector3.one;
                root.localScale = Vector3.one * 3;
                bool saved;
                PrefabUtility.SaveAsPrefabAsset(prefab, Path, out saved);
                if (!saved) throw new InvalidOperationException("MGAA 炮塔预制体保存失败。");
                AssetImporter.GetAtPath(Path).SetAssetBundleNameAndVariant("ssr_combat_windows", "");
                AssetDatabase.SaveAssets();
                return "MGAA 已建立共用偏航、近防炮俯仰、固定导弹筒、八枪管、八枚导弹和后部扫描雷达；底座三乘三。";
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        //在原模型坐标中建立关节。
        private static Transform Group(Transform parent, Transform reference, string name, Vector3 point)
        {
            var transform = new GameObject(name).transform;
            transform.SetParent(parent, false);
            transform.position = reference.TransformPoint(point);
            transform.rotation = reference.rotation;
            return transform;
        }

        //建立朝向模型负 Z 轴的发射挂点。
        private static void FirePoint(Transform parent, Transform reference, string name, Vector3 point)
        {
            var fire = Group(parent, reference, name, point);
            fire.rotation = reference.rotation * Quaternion.Euler(0, 180, 0);
        }

        //计算网格在模型坐标中的范围。
        private static Bounds LocalBounds(Transform reference, MeshFilter filter)
        {
            var vertices = filter.sharedMesh.vertices;
            var bounds = new Bounds(reference.InverseTransformPoint(filter.transform.TransformPoint(vertices[0])), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(reference.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            return bounds;
        }

        //通过 AssetDatabase 创建资源目录。
        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
