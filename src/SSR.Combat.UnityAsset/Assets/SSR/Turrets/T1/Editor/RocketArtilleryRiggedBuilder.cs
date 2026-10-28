using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SSR.Combat.Editor
{
    //将单贴图刚性骨架接到现有火箭炮挂点，保留库存弹体和发射配置。
    internal static class RocketArtilleryRiggedBuilder
    {
        private const string ModelPath = T1ModelTools.BasePath + "/Models/RocketArtillery_Rigged.fbx";
        private const string MaterialPath = T1ModelTools.BasePath + "/Materials/RocketArtillery_Baked.mat";
        private const string ExistingPrefab = T1ModelTools.BasePath + "/Game/RocketArtillery.prefab";
        private const string OutputPrefab = T1ModelTools.BasePath + "/Game/RocketArtillery_Rigged.prefab";
        private const string MeshPath = T1ModelTools.BasePath + "/Models/RocketArtillery_RiggedRuntime.asset";

        //生成独立游戏预制体，不改变当前建筑 Def 引用。
        [MenuItem("SSR/炮塔资源/生成单贴图刚性骨骼火箭炮")]
        public static void Build()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material || !material.mainTexture)
                throw new InvalidOperationException("缺少火箭炮烘焙材质或颜色贴图。");

            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 1;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "RocketArtillery_Baked"), material);
            importer.SaveAndReimport();

            GameObject source = null, target = null;
            try
            {
                source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
                target = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ExistingPrefab));
                target.name = "RocketArtillery_Rigged";
                var sourceSkin = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                var sourceRoot = source.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Root");
                var targetRoot = Require(target.transform, "Root");
                var intoRoot = sourceRoot.worldToLocalMatrix;
                var rotationIntoRoot = Quaternion.Inverse(sourceRoot.rotation);

                //沿父子顺序更新关节，原来的 Missile 和逐弹 FirePoint 保留在弹位下。
                foreach (var bone in sourceRoot.GetComponentsInChildren<Transform>(true)
                    .Where(t => t != sourceRoot).OrderBy(t => BonePath(t, sourceRoot).Count(c => c == '/')))
                {
                    var destination = Require(target.transform, BonePath(bone, sourceRoot));
                    destination.SetPositionAndRotation(
                        targetRoot.TransformPoint(intoRoot.MultiplyPoint3x4(bone.position)),
                        targetRoot.rotation * rotationIntoRoot * bone.rotation);
                }

                var slots = Require(target.transform, "Root/Yaw/Pitch/Slots");
                foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!filter.transform.IsChildOf(slots)) Object.DestroyImmediate(filter.gameObject);
                }

                var body = new GameObject("BodyMesh");
                body.transform.SetParent(targetRoot, false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                var bones = sourceSkin.bones.Select(b => Require(target.transform, BonePath(b, sourceRoot))).ToArray();
                var mesh = BuildMesh(sourceSkin, intoRoot, bones, body.transform);
                var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (existingMesh)
                {
                    EditorUtility.CopySerialized(mesh, existingMesh);
                    Object.DestroyImmediate(mesh);
                    mesh = existingMesh;
                }
                else AssetDatabase.CreateAsset(mesh, MeshPath);

                skin.sharedMesh = mesh;
                skin.sharedMaterial = material;
                skin.bones = bones;
                skin.rootBone = targetRoot;
                skin.quality = SkinQuality.Bone1;
                skin.updateWhenOffscreen = true;
                skin.localBounds = mesh.bounds;
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.receiveShadows = false;
                ValidateAttachments(target.transform);
                EditorUtility.SetDirty(mesh);
                var prefab = T1ModelTools.Save(target, OutputPrefab);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log("已生成刚性骨骼火箭炮：一个炮塔网格、一个材质、十个筒盖，保留十枚独立库存弹体。路径：" + OutputPrefab);
            }
            finally
            {
                if (source) Object.DestroyImmediate(source);
                if (target) Object.DestroyImmediate(target);
            }
        }

        //将 FBX 网格统一到游戏 Root 坐标，并重建绑定矩阵。
        private static Mesh BuildMesh(SkinnedMeshRenderer source, Matrix4x4 intoRoot, Transform[] bones, Transform target)
        {
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = "RocketArtillery_Rigged";
            if (mesh.subMeshCount != 1 || mesh.boneWeights.Length != mesh.vertexCount)
                throw new InvalidOperationException("火箭炮必须是单子网格且所有顶点都有刚性权重。");
            foreach (var weight in mesh.boneWeights)
            {
                if (Mathf.Abs(weight.weight0 - 1) > 0.00001f
                    || weight.weight1 != 0 || weight.weight2 != 0 || weight.weight3 != 0)
                    throw new InvalidOperationException("火箭炮包含混合权重，机械部件不能使用平滑蒙皮。");
            }

            var matrix = intoRoot * source.transform.localToWorldMatrix;
            var normalMatrix = matrix.inverse.transpose;
            mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            mesh.normals = mesh.normals.Select(n => normalMatrix.MultiplyVector(n).normalized).ToArray();
            mesh.tangents = Array.Empty<Vector4>();
            if (matrix.determinant < 0)
            {
                var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int swap = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = swap;
                }
                mesh.triangles = triangles;
            }
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * target.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        //骨骼名称与现有 XML 的路径完全对应。
        private static string BonePath(Transform bone, Transform root)
        {
            return bone == root ? "Root" : "Root/" + AnimationUtility.CalculateTransformPath(bone, root);
        }

        //缺少配置挂点时直接报告具体路径。
        private static Transform Require(Transform root, string path)
        {
            var result = root.Find(path);
            if (!result) throw new InvalidOperationException("刚性火箭炮缺少挂点：" + path);
            return result;
        }

        //确保独立弹体与筒盖在合并炮塔后仍能被运行逻辑找到。
        private static void ValidateAttachments(Transform model)
        {
            Require(model, "Root/Yaw/Pitch/FirePoint");
            for (int i = 1; i <= 10; i++)
            {
                string suffix = i.ToString("D2");
                Require(model, "Root/Yaw/Pitch/Doors/Door" + suffix);
                Require(model, "Root/Yaw/Pitch/Slots/Slot" + suffix + "/Missile");
                Require(model, "Root/Yaw/Pitch/Slots/Slot" + suffix + "/FirePoint");
            }
        }
    }
}
