using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SSR.Combat.Editor
{
    //将绘制后的单网格炮塔接到独立刚性关节，并替换原游戏资源。
    internal static class Railgun145RiggedBuilder
    {
        private const string Folder = T1ModelTools.BasePath;
        private const string PrefabPath = "Assets/SSR/Prefab/TurretT1_3X.prefab";
        private const string BindingPath = "ArtSource/T1Turrets/Bindings/Railgun145_Rigged.json";

        //读取 Blender 导出的关节位置和父子关系。
        [Serializable]
        private sealed class RigData
        {
            public BoneData[] nodes;
            public int triangles;
            public int degenerateTriangles;
            public float surfaceArea;
        }

        //骨骼位置采用 Blender 的米制模型坐标。
        [Serializable]
        private sealed class BoneData
        {
            public string name;
            public string parent;
            public float[] head;
        }

        //导入贴图和骨架，保留原预制体 GUID，供现有建筑直接加载。
        [MenuItem("SSR/炮塔资源/接入单贴图刚性骨骼145mm炮塔")]
        public static void Build()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/Railgun145_Baked.mat");
            if (!material || !material.mainTexture || material.mainTexture.width != 8192)
                throw new InvalidOperationException("145mm 单贴图材质缺失，或颜色图未按 8K 导入。");
            if (!material.shader || material.shader.name != "SSR/Turrets/BakedColor")
                throw new InvalidOperationException("145mm 炮塔必须使用公共底色材质，光照由游戏离屏管线计算。");
            string modelPath = Folder + "/Models/Railgun145_Rigged.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false;
            importer.optimizeGameObjects = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 1;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Railgun145_Baked"), material);
            importer.SaveAndReimport();

            var layout = JsonUtility.FromJson<RigData>(File.ReadAllText(BindingPath, Encoding.UTF8));
            GameObject source = null, target = null;
            try
            {
                source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath));
                var sourceBones = source.GetComponentsInChildren<Transform>(true).ToDictionary(t => t.name);
                var sourceSkin = source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
                var definitions = layout.nodes.ToDictionary(n => n.name);
                var conversion = CoordinateMap(sourceBones, definitions);
                foreach (var node in layout.nodes)
                    if (Vector3.Distance(conversion.MultiplyPoint3x4(sourceBones[node.name].position), Position(node)) > .00002f)
                        throw new InvalidOperationException("FBX 骨骼位置与导出记录不一致：" + node.name);

                target = new GameObject("TurretT1_3X");
                var bones = new Dictionary<string, Transform>();
                foreach (var node in layout.nodes)
                {
                    var bone = new GameObject(node.name).transform;
                    bone.SetParent(string.IsNullOrEmpty(node.parent) ? target.transform : bones[node.parent], false);
                    bone.SetPositionAndRotation(Position(node), Quaternion.identity);
                    bones.Add(node.name, bone);
                }
                var body = new GameObject("BodyMesh");
                body.transform.SetParent(bones["Root"], false);
                var skin = body.AddComponent<SkinnedMeshRenderer>();
                skin.bones = sourceSkin.bones.Select(b => bones[b.name]).ToArray();
                var mesh = ConvertMesh(sourceSkin, conversion, skin, layout);
                string meshPath = Folder + "/Models/Railgun145_RiggedRuntime.asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                skin.sharedMesh = mesh;
                skin.sharedMaterial = material;
                skin.rootBone = bones["Root"];
                skin.quality = SkinQuality.Bone1;
                skin.updateWhenOffscreen = true;
                skin.localBounds = mesh.bounds;
                skin.shadowCastingMode = ShadowCastingMode.Off;
                skin.receiveShadows = false;
                AddRecoil(bones["Pitch"]);
                //地图绘制会覆盖预制体根缩放；纵向倍率和原来的抬升保留在模型 Root。
                bones["Root"].localPosition = new Vector3(0, 1.5f, 0);
                bones["Root"].localScale = new Vector3(1, 3, 1);
                target.transform.localScale = new Vector3(3, 1, 3);
                Validate(target, skin);
                T1ModelTools.Save(target, PrefabPath);
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssets();
                Debug.Log("SSR145 接入完成：一个蒙皮网格、一个 8K 材质、18 根骨骼；顶点=" + mesh.vertexCount
                    + "，三角形=" + mesh.triangles.Length / 3 + "，局部范围=" + mesh.bounds
                    + "；已替换 " + PrefabPath);
            }
            finally
            {
                if (source) Object.DestroyImmediate(source);
                if (target) Object.DestroyImmediate(target);
            }
        }

        //统一成 Unity 的 Y 向上、Z 向前坐标，骨骼局部旋转全部归零。
        private static Vector3 Position(BoneData node) => new Vector3(node.head[0], node.head[2], node.head[1]);

        //用四个已知关节消除 FBX 导入的轴交换与单位缩放，随后逐骨骼核验。
        private static Matrix4x4 CoordinateMap(Dictionary<string, Transform> source, Dictionary<string, BoneData> target)
        {
            string[] names = { "Root", "Yaw", "Pitch", "SensorPitch" };
            var from = new Matrix4x4();
            var to = new Matrix4x4();
            for (int i = 0; i < names.Length; i++)
            {
                Vector3 a = source[names[i]].position, b = Position(target[names[i]]);
                from.SetColumn(i, new Vector4(a.x, a.y, a.z, 1));
                to.SetColumn(i, new Vector4(b.x, b.y, b.z, 1));
            }
            if (Mathf.Abs(from.determinant) < 1e-9f)
                throw new InvalidOperationException("145mm 导入骨架缺少独立的空间轴。");
            return to * from.inverse;
        }

        //将几何和法线转换到模型 Root 坐标，按当前关节重建绑定矩阵。
        private static Mesh ConvertMesh(SkinnedMeshRenderer source, Matrix4x4 conversion, SkinnedMeshRenderer target, RigData layout)
        {
            var mesh = Object.Instantiate(source.sharedMesh);
            mesh.name = "Railgun145_Rigged";
            if (mesh.subMeshCount != 1 || mesh.boneWeights.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount)
                throw new InvalidOperationException("145mm 网格缺少完整的刚性权重或法线，或包含多个子网格。");
            foreach (var weight in mesh.boneWeights)
                if (Mathf.Abs(weight.weight0 - 1) > .00001f || weight.weight1 != 0 || weight.weight2 != 0 || weight.weight3 != 0)
                    throw new InvalidOperationException("145mm 网格包含混合权重。");
            var matrix = conversion * source.transform.localToWorldMatrix;
            var normals = matrix.inverse.transpose;
            mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            mesh.normals = mesh.normals.Select(n => normals.MultiplyVector(n).normalized).ToArray();
            mesh.tangents = Array.Empty<Vector4>();
            //Unity 会清理源模型的退化面，核对有效表面积，避免将正常清理误判为丢面。
            var indices = mesh.triangles;
            var vertices = mesh.vertices;
            double area = 0;
            for (int i = 0; i < indices.Length; i += 3)
                area += Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]],
                    vertices[indices[i + 2]] - vertices[indices[i]]).magnitude * .5;
            int removed = layout.triangles - indices.Length / 3;
            if (removed < 0 || removed > layout.degenerateTriangles || Math.Abs(area - layout.surfaceArea) > layout.surfaceArea * .00001)
                throw new InvalidOperationException("145mm 有效几何不一致：三角形=" + indices.Length / 3
                    + "，面积=" + area + "，源面积=" + layout.surfaceArea);
            Debug.Log("SSR145 几何核对：移除退化面=" + removed + "，有效表面积=" + area);
            if (matrix.determinant < 0)
            {
                var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                mesh.triangles = triangles;
            }
            mesh.bindposes = target.bones.Select(b => b.worldToLocalMatrix * target.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        //后坐仅修改炮管子骨骼的位置，不覆盖主炮俯仰或转台方向。
        private static void AddRecoil(Transform pitch)
        {
            string path = Folder + "/Animations/Railgun145RigidRecoil.anim";
            var clip = new AnimationClip { name = "Railgun145Recoil", frameRate = 60 };
            foreach (var item in new[] { ("RecoilOuter", .125f), ("RecoilInner", .15f) })
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(item.Item1, typeof(Transform), "m_LocalPosition.z"),
                    new AnimationCurve(new Keyframe(0, 0, 0, -item.Item2 * 12),
                        new Keyframe(1f / 12, -item.Item2, 0, 0), new Keyframe(.5f, 0, 0, 0)));
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(clip, existing);
                Object.DestroyImmediate(clip);
                clip = existing;
            }
            else AssetDatabase.CreateAsset(clip, path);
            string controllerPath = Folder + "/Animations/Railgun145RigidRecoil.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            if (controller.layers.Length == 0) controller.AddLayer("Base Layer");
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Length == 0 ? machine.AddState("后坐") : machine.states[0].state;
            state.motion = clip;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            var animator = pitch.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.enabled = false;
            EditorUtility.SetDirty(clip);
            EditorUtility.SetDirty(controller);
        }

        //保存前检查游戏实际访问的挂点和资源，防止部署缺失路径的预制体。
        private static void Validate(GameObject prefab, SkinnedMeshRenderer skin)
        {
            string[] paths = { "Root", "Root/Yaw", "Root/Yaw/Pitch", "Root/Yaw/Pitch/RecoilOuter",
                "Root/Yaw/Pitch/RecoilInner/FirePoint", "Root/Yaw/RadarPitch" };
            foreach (string path in paths)
                if (!prefab.transform.Find(path)) throw new InvalidOperationException("145mm 缺少游戏挂点：" + path);
            if (prefab.GetComponentsInChildren<Renderer>(true).Length != 1 || skin.sharedMaterials.Length != 1)
                throw new InvalidOperationException("145mm 应只有一个 Renderer 和一个材质。");
            var controller = prefab.transform.Find("Root/Yaw/Pitch").GetComponent<Animator>().runtimeAnimatorController;
            if (!controller.animationClips.Any(c => c.name == "Railgun145Recoil" && c.length > 0))
                throw new InvalidOperationException("145mm 后坐片段没有正确挂入控制器。");
            Debug.Log("SSR145 挂点核对完成：炮口=" + prefab.transform.Find(paths[4]).position
                + "，前向=" + prefab.transform.Find(paths[4]).forward + "，骨骼数=" + skin.bones.Length);
        }
    }
}
