using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace SSR.UnityComponent.Outline.Editor
{
    //从实际游戏预制体导出炮塔的透明菜单图标，复用游戏描边和表面光照。
    internal static class TurretIconExporter
    {
        //读取当前建筑配置，按默认姿态逐个生成 PNG，不进入播放模式。
        [MenuItem("SSR/炮塔资源/导出当前炮塔图标")]
        public static void Export()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string directory = Path.Combine(projectRoot, "1.6/Defs/ThingDefs/Bulidings");
            var definitions = Directory.GetFiles(directory, "*.xml").SelectMany(source => XDocument.Load(source).Root.Elements("ThingDef"))
                .Where(definition => definition.Element("thingClass")?.Value == "SSR.Combat.Offscreen.Building_ConfigurableTurret"
                    || definition.Element("thingClass")?.Value == "SSR.Combat.Offscreen.Building_CombinedAirDefense");
            var report = new StringBuilder();
            var framing = new XElement("TurretIconFraming");
            foreach (var definition in definitions)
            {
                framing.Add(Capture(definition, projectRoot));
                report.AppendLine(definition.Element("defName").Value + "：PNG 已生成。");
            }
            string reportPath = Path.Combine(projectRoot, ".local/build-requests/turret-icons.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            new XDocument(framing).Save(Path.ChangeExtension(reportPath, ".xml"));
            Debug.Log(report.ToString());
        }

        //使用 XML 中的游戏模型、外层比例和默认关节姿态采集单座炮塔。
        private static XElement Capture(XElement definition, string projectRoot)
        {
            var prefabSettings = definition.Element("comps").Elements("li")
                .Single(element => element.Attribute("Class")?.Value == "SSR.Combat.Offscreen.OffscreenPrefabProperties");
            var aimSettings = definition.Element("modExtensions").Elements("li")
                .Single(element => element.Attribute("Class")?.Value == "SSR.Combat.Offscreen.TurretAimSettings");
            string prefabPath = prefabSettings.Element("leveledPrefabPaths").Element("li").Value;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) throw new InvalidOperationException("炮塔预制体不存在：" + prefabPath);
            var model = UnityEngine.Object.Instantiate(prefab);
            var cameraObject = new GameObject("炮塔图标离屏相机");
            var output = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            var previous = RenderTexture.active;
            try
            {
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var root = model.transform.Find(prefabSettings.Element("modelRootPath").Value);
                if (!root) throw new InvalidOperationException("炮塔模型根节点不存在：" + prefabPath);
                root.localRotation = Quaternion.identity;
                var size = ReadVector(definition.Element("graphicData").Element("drawSize").Value);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = new Vector3(size.x, 1, size.y);
                ApplyIdle(model, aimSettings, "yawPath", "yawRotationAixe", "idleYaw");
                ApplyIdle(model, aimSettings, "pitchPath", "pitchRotationAixe", "idlePitch");
                cameraObject.AddComponent<Camera>();
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
                capture.surfaceLighting = true;
                cameraObject.transform.rotation = Quaternion.Euler(60, 0, 0);
                capture.FrameTarget();
                capture.RenderNow();
                RenderTexture.active = output;
                texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                texture.Apply();
                string destination = Path.Combine(projectRoot, "1.6/Textures", definition.Element("uiIconPath").Value + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllBytes(destination, texture.EncodeToPNG());
                //记录透明画布的地图尺寸与底面偏移，蓝图采用同一图像时保留实际模型比例。
                var camera = cameraObject.GetComponent<Camera>();
                Vector3 anchor = new Vector3(0, FloorHeight(model), 0);
                Vector3 delta = camera.transform.position - anchor;
                float mapSize = camera.orthographicSize * 2;
                return new XElement("Turret", new XAttribute("defName", definition.Element("defName").Value),
                    new XElement("drawSize", "(" + Number(mapSize) + "," + Number(mapSize) + ")"),
                    new XElement("drawOffset", "(" + Number(Vector3.Dot(delta, camera.transform.right)) + ",0,"
                        + Number(Vector3.Dot(delta, camera.transform.up)) + ")"));
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(model);
                output.Release();
                UnityEngine.Object.DestroyImmediate(output);
                if (texture) UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        //查找实体模型底面高度，与游戏的底座锚点保持一致。
        private static float FloorHeight(GameObject model)
        {
            return model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled
                && !renderer.sharedMaterials.Any(material => material && material.shader && material.shader.name.Contains("Wireframe")))
                .Min(renderer => renderer.bounds.min.y);
        }

        //输出 XML 使用的固定小数格式，避免系统区域设置影响向量解析。
        private static string Number(float value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        //按配置的局部转轴设置默认角度，保留模型导入时的关节基础旋转。
        private static void ApplyIdle(GameObject model, XElement settings, string pathField, string axisField, string angleField)
        {
            var joint = model.transform.Find(settings.Element(pathField).Value);
            if (!joint) throw new InvalidOperationException("炮塔关节不存在：" + settings.Element(pathField).Value);
            float angle = (float?)settings.Element(angleField) ?? 0;
            joint.localRotation *= Quaternion.AngleAxis(angle, ReadVector(settings.Element(axisField).Value));
        }

        //读取建筑配置中的二维或三维数值向量，统一按固定小数格式解析。
        private static Vector3 ReadVector(string value)
        {
            var numbers = value.Trim('(', ')').Split(',')
                .Select(number => float.Parse(number, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new Vector3(numbers[0], numbers[1], numbers.Length == 3 ? numbers[2] : 0);
        }
    }
}
