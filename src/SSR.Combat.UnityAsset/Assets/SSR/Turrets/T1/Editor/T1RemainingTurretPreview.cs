using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SSR.UnityComponent.Outline;
using UnityEditor;
using UnityEngine;

namespace SSR.Combat.Editor
{
    //按 XML 配置导出炮塔机构和弹体预览。
    internal static class T1RemainingTurretPreview
    {
        //校验机械挂点和弹体资源，导出俯仰及离筒预览。
        [MenuItem("SSR/炮塔资源/预览剩余五种炮塔")]
        public static void Export()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string directory = Path.Combine(project, "Docs/Previews/T1Integration");
            Directory.CreateDirectory(directory);
            foreach (string name in new[] { "EMSentry", "PortableEMSentry", "EMAutocannon", "RocketArtillery", "MissileBox" })
            {
                var definition = XDocument.Load(Path.Combine(project, "1.6/Defs/ThingDefs/Bulidings/Buildings_" + name + ".xml"))
                    .Root.Elements("ThingDef").First();
                var properties = definition.Element("comps").Elements("li").Single(e => (string)e.Attribute("Class") == "SSR.Combat.Offscreen.OffscreenPrefabProperties");
                var extensions = definition.Element("modExtensions").Elements("li");
                var aim = extensions.Single(e => (string)e.Attribute("Class") == "SSR.Combat.Offscreen.TurretAimSettings");
                var launcher = extensions.SingleOrDefault(e => (string)e.Attribute("Class") == "SSR.Combat.Offscreen.TubeLauncherSettings");
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(properties.Element("leveledPrefabPaths").Element("li").Value));
                try
                {
                    foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
                    foreach (string field in new[] { "rootTransform", "yawPath", "pitchPath", "shootingOrigine" })
                        Require(model, aim.Element(field).Value);
                    var pitch = Require(model, aim.Element("pitchPath").Value);
                    var fire = Require(model, aim.Element("shootingOrigine").Value);
                    var axis = Vector(aim.Element("pitchRotationAixe").Value);
                    pitch.localRotation *= Quaternion.AngleAxis(35, axis);
                    if (fire.forward.y < 0.5f) throw new InvalidOperationException(name + " 俯仰轴未使真实炮口向上。");
                    if (launcher != null)
                    {
                        var payload = AssetDatabase.LoadAssetAtPath<GameObject>(launcher.Element("missilePrefabPath").Value);
                        foreach (var slotPath in launcher.Element("missileSlots").Elements("li"))
                        {
                            var missile = Require(model, slotPath.Value + "/Missile");
                            var muzzle = Require(model, slotPath.Value + "/FirePoint");
                            if (Vector3.Distance(missile.Find("TipPoint").position, muzzle.position) > 0.0001f
                                || Quaternion.Angle(missile.rotation, muzzle.rotation) > 0.001f)
                                throw new InvalidOperationException(name + " 库存弹头与发射点不重合：" + slotPath.Value);
                            if (missile.GetComponent<MeshFilter>().sharedMesh != payload.GetComponent<MeshFilter>().sharedMesh)
                                throw new InvalidOperationException(name + " 库存弹体与飞行网格不一致。");
                        }
                        foreach (var door in launcher.Element("doors").Elements("li"))
                            Require(model, door.Element("path").Value).localRotation *= Quaternion.AngleAxis(
                                (float)door.Element("openAngle"), Vector(door.Element("axis").Value));
                    }
                    float yaw = Mathf.Atan2(fire.forward.x, fire.forward.z) * Mathf.Rad2Deg;
                    model.transform.rotation = Quaternion.Euler(0, 180 - yaw, 0);
                    Render(model, Path.Combine(directory, name + "-Pitch.png"));
                    if (launcher != null)
                    {
                        string slot = launcher.Element("missileSlots").Element("li").Value;
                        var missile = Require(model, slot + "/Missile");
                        var muzzle = Require(model, slot + "/FirePoint");
                        var flying = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(launcher.Element("missilePrefabPath").Value));
                        flying.transform.SetParent(model.transform, false);
                        flying.transform.SetPositionAndRotation(missile.position, missile.rotation);
                        flying.transform.localScale = missile.lossyScale;
                        float length = Vector3.Distance(missile.Find("TipPoint").position, missile.Find("TrailPoint").position);
                        flying.transform.position += muzzle.forward * (length + 0.25f);
                        missile.gameObject.SetActive(false);
                        Render(model, Path.Combine(directory, name + "-Payload.png"));
                    }
                    Debug.Log(name + "：关节、弹位、弹头位置及库存/飞行网格核对通过。");
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }

        //使用炮塔离屏采集组件导出预览。
        private static void Render(GameObject model, string destination)
        {
            var cameraObject = new GameObject("T1机械预览相机");
            var output = new RenderTexture(768, 768, 0, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(768, 768, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                cameraObject.AddComponent<Camera>();
                var capture = cameraObject.AddComponent<TurretOutlineCapture>();
                capture.continuous = false;
                capture.target = model.transform;
                capture.output = output;
                capture.geometryShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SSR/Outline/Shaders/TurretGeometry.shader");
                capture.compositeShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SSR/Outline/Shaders/TurretOutline.shader");
                capture.orthographicProjection = true;
                capture.surfaceLighting = true;
                capture.supersampling = 2;
                capture.silhouetteWidth = 3.7f;
                capture.structureWidth = 0;
                cameraObject.transform.rotation = Quaternion.Euler(35, 55, 0);
                capture.FrameTarget();
                capture.RenderNow();
                RenderTexture.active = output;
                texture.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                texture.Apply();
                File.WriteAllBytes(destination, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(texture);
                output.Release();
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        //将 XML 中的局部轴转换为 Unity 向量。
        private static Vector3 Vector(string value)
        {
            var parts = value.Trim('(', ')').Split(',').Select(v => float.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new Vector3(parts[0], parts[1], parts[2]);
        }

        //查找机械挂点，缺失时报告路径。
        private static Transform Require(GameObject model, string path)
        {
            var result = model.transform.Find(path);
            if (!result) throw new InvalidOperationException(model.name + " 缺少机械挂点：" + path);
            return result;
        }
    }
}
