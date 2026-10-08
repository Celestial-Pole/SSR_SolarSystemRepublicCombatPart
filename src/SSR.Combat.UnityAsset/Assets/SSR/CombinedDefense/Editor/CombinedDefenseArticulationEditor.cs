using System;
using System.IO;
using System.Linq;
using SSR.UnityComponent.Outline;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //拆分并预览弹炮合一的俯仰和雷达机构。
    internal static class CombinedDefenseArticulationEditor
    {
        private const string PrefabPath = "Assets/SSR/Prefab/TurretT1_MGAA_2X.prefab";

        //将共用俯仰层级拆分为独立机构。
        [MenuItem("SSR/炮塔资源/拆分弹炮合一独立机构")]
        public static void Apply()
        {
            var model = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var root = Require(model.transform, "Root");
                var yaw = Require(root, "Yaw");
                if (yaw.Find("MissilePitch")) throw new InvalidOperationException("弹炮合一已采用独立俯仰层级。");
                var left = Require(yaw, "Pitch/RackLeft");
                var right = Require(yaw, "Pitch/RackRight");
                var sensorLeft = Require(yaw, "RadarLeft");
                var sensorRight = Require(yaw, "RadarRight");
                string[] radarNames = { "立方体.006", "立方体.007", "立方体.008", "立方体.009", "立方体.010" };
                var radarMeshes = radarNames.Select(name => Require(yaw, name)).ToArray();
                var missilePitch = Joint(yaw, root, "MissilePitch", new Vector3(0, 0.326f, 0.405f));
                left.SetParent(missilePitch, true);
                right.SetParent(missilePitch, true);
                Require(yaw, "立方体.014").SetParent(missilePitch, true);
                Require(yaw, "Pitch/立方体.027").SetParent(yaw, true);
                var aimPoint = Joint(missilePitch, root, "AimPoint", new Vector3(0, 0.326f, -0.510f));
                aimPoint.rotation = root.rotation * Quaternion.Euler(0, 180, 0);
                sensorLeft.name = "SensorLeft";
                sensorRight.name = "SensorRight";
                var radar = Joint(yaw, root, "RadarRear", new Vector3(0, 0.42808f, 0.38262f));
                foreach (var mesh in radarMeshes) mesh.SetParent(radar, true);
                PrefabUtility.SaveAsPrefabAsset(model, PrefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("弹炮合一独立机构保存失败。");
                AssetDatabase.SaveAssets();
                Debug.Log("弹炮合一：近防炮和导弹已拆分俯仰；后部扁平雷达独立旋转；前方小部件固定。");
            }
            finally { PrefabUtility.UnloadPrefabContents(model); }
            ExportPreview();
        }

        //分别导出近防炮和导弹架抬升的机构预览。
        [MenuItem("SSR/炮塔资源/预览弹炮合一独立机构")]
        public static void ExportPreview()
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var cameraObject = new GameObject("弹炮合一机构预览相机");
            var output = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var gun = Require(model.transform, "Root/Yaw/Pitch");
                var missiles = Require(model.transform, "Root/Yaw/MissilePitch");
                var radar = Require(model.transform, "Root/Yaw/RadarRear");
                var camera = cameraObject.AddComponent<Camera>();
                var capture = cameraObject.AddComponent<TurretOutlineCapture>();
                capture.continuous = false;
                capture.target = model.transform;
                capture.output = output;
                capture.geometryShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SSR/Outline/Shaders/TurretGeometry.shader");
                capture.compositeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SSR/Outline/Shaders/TurretOutline.shader");
                capture.orthographicProjection = true;
                capture.supersampling = 2;
                capture.silhouetteWidth = 3.7f;
                capture.structureWidth = 0;
                cameraObject.transform.rotation = Quaternion.Euler(40, 55, 0);
                capture.FrameTarget();
                camera.orthographicSize *= 1.05f;
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../Docs/Previews"));
                Directory.CreateDirectory(directory);
                for (int i = 0; i < 2; i++)
                {
                    gun.localRotation = Quaternion.Euler(i == 0 ? 45 : 0, 0, 0);
                    missiles.localRotation = Quaternion.Euler(i == 1 ? 45 : 0, 0, 0);
                    radar.localRotation = Quaternion.Euler(0, i * 90, 0);
                    capture.RenderNow();
                    RenderTexture.active = output;
                    texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    texture.Apply();
                    string name = i == 0 ? "CombinedAirDefense-GunPitch.png" : "CombinedAirDefense-MissilePitch.png";
                    File.WriteAllBytes(Path.Combine(directory, name), texture.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(model);
                output.Release();
                UnityEngine.Object.DestroyImmediate(output);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        //按原模型坐标放置关节，计入根节点缩放。
        private static Transform Joint(Transform parent, Transform root, string name, Vector3 position)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.SetPositionAndRotation(root.TransformPoint(position), root.rotation);
            return joint;
        }

        //查找机械节点，缺失时报告路径。
        private static Transform Require(Transform root, string path)
        {
            var result = root.Find(path);
            if (!result) throw new InvalidOperationException("弹炮合一缺少机械节点：" + path);
            return result;
        }
    }
}
