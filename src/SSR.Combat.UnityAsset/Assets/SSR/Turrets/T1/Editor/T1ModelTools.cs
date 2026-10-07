using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //提供炮塔关节、资源保存和后坐动画的构建工具。
    internal static class T1ModelTools
    {
        internal const string BasePath = "Assets/SSR/Turrets/T1";

        //实例化源预制体并移除 Animator。
        internal static GameObject Load(string name)
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BasePath + "/Prefabs/" + name + ".prefab"));
            model.name = name;
            foreach (var animator in model.GetComponentsInChildren<Animator>(true)) UnityEngine.Object.DestroyImmediate(animator);
            return model;
        }

        //计算网格在模型坐标中的范围。
        internal static Bounds Bounds(Transform reference, MeshFilter filter)
        {
            var vertices = filter.sharedMesh.vertices;
            var bounds = new Bounds(reference.InverseTransformPoint(filter.transform.TransformPoint(vertices[0])), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(reference.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            return bounds;
        }

        //在模型坐标中创建关节。
        internal static Transform Joint(Transform parent, Transform reference, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.SetPositionAndRotation(reference.TransformPoint(position), reference.rotation);
            return node;
        }

        //保存预制体并设置 Windows 资源包标签。
        internal static GameObject Save(GameObject model, string path)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var result = PrefabUtility.SaveAsPrefabAsset(model, path, out bool saved);
            if (!saved) throw new InvalidOperationException("T1 预制体保存失败：" + path);
            AssetImporter.GetAtPath(path).SetAssetBundleNameAndVariant("ssr_combat_windows", "");
            return result;
        }

        //生成后坐节点的位移动画。
        internal static void AddRecoil(Transform node, string name, float distance)
        {
            string directory = BasePath + "/Animations";
            EnsureFolder(directory);
            string clipPath = directory + "/" + name + "Recoil.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (!clip) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
            clip.name = name + "Recoil";
            clip.frameRate = 60;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"),
                new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.04f, -distance), new Keyframe(0.16f, 0)));
            string controllerPath = directory + "/" + name + "Recoil.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.layers[0].stateMachine.AddState("后坐").motion = clip;
            }
            node.gameObject.AddComponent<Animator>().runtimeAnimatorController = controller;
            EditorUtility.SetDirty(clip);
        }

        //通过 AssetDatabase 创建资源目录。
        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
