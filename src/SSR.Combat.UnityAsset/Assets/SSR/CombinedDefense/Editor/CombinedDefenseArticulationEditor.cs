using System;
using System.IO;
using SSR.UnityComponent.Outline;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //预览弹炮合一的近防炮俯仰和固定导弹架。
    internal static class CombinedDefenseArticulationEditor
    {
        private const string PrefabPath = "Assets/SSR/Prefab/TurretT1_MGAA_2X.prefab";

        //导出近防炮水平和抬升时的机构预览。
        [MenuItem("SSR/炮塔资源/预览弹炮合一机构")]
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
                Require(model.transform, "Root/Yaw/MissileRack");
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
                    gun.localRotation = Quaternion.Euler(i * 60, 0, 0);
                    radar.localRotation = Quaternion.Euler(0, i * 90, 0);
                    capture.RenderNow();
                    RenderTexture.active = output;
                    texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    texture.Apply();
                    string name = i == 0 ? "CombinedAirDefense-Level.png" : "CombinedAirDefense-GunPitch.png";
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

        //查找机械节点，缺失时报告路径。
        private static Transform Require(Transform root, string path)
        {
            var result = root.Find(path);
            if (!result) throw new InvalidOperationException("弹炮合一缺少机械节点：" + path);
            return result;
        }
    }
}
