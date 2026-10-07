using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using SSR.UnityComponent.Missiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSR.UnityComponent.Outline.Editor
{
    //从资源包导出炮塔菜单图标和放置蓝图。
    internal static class TurretIconExporter
    {
        //导出菜单图标并同步放置蓝图。
        [MenuItem("SSR/炮塔资源/导出当前炮塔图标")]
        public static void Export() => Export(true);

        //同步放置蓝图及其尺寸和偏移。
        [MenuItem("SSR/炮塔资源/同步蓝图与实际模型")]
        public static void ExportBlueprints() => Export(false);

        //加载资源包并将蓝图取景参数写回 XML。
        private static void Export(bool exportMenu)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            string directory = Path.Combine(projectRoot, "1.6/Defs/ThingDefs/Bulidings");
            var documents = Directory.GetFiles(directory, "*.xml")
                .Select(path => new { Path = path, Document = XDocument.Load(path, LoadOptions.PreserveWhitespace) }).ToArray();
            var definitions = documents.SelectMany(source => source.Document.Root.Elements("ThingDef"))
                .Where(definition => definition.Element("thingClass")?.Value == "SSR.Combat.Offscreen.Building_ConfigurableTurret"
                    || definition.Element("thingClass")?.Value == "SSR.Combat.Offscreen.Building_CombinedAirDefense"
                    || definition.Element("thingClass")?.Value == "SSR.Combat.Offscreen.Building_TubeMissileTurret"
                    || definition.Element("thingClass")?.Value == "SSR.Combat.Offscreen.Building_MissileSilo");
            var report = new StringBuilder();
            var framing = new XElement("TurretIconFraming");
            var bundle = AssetBundle.LoadFromFile(Path.Combine(projectRoot, "Asset/Windows/ssr_combat_windows"));
            if (!bundle) throw new InvalidOperationException("无法读取已构建的 Windows 战斗资源包。");
            try
            {
                foreach (var definition in definitions)
                {
                    framing.Add(Capture(definition, projectRoot, bundle, exportMenu));
                    report.AppendLine(definition.Element("defName").Value + "：蓝图 PNG 与 XML 尺寸已同步。");
                }
            }
            finally { bundle.Unload(true); }
            foreach (var document in documents.Where(source => source.Document.Root.Elements("ThingDef").Any(definitions.Contains)))
                File.WriteAllText(document.Path, document.Document.Declaration + Environment.NewLine
                    + document.Document.ToString(SaveOptions.DisableFormatting).TrimStart('\r', '\n'), new UTF8Encoding(false));
            string reportPath = Path.Combine(projectRoot, ".local/build-requests/turret-icons.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(false));
            new XDocument(framing).Save(Path.ChangeExtension(reportPath, ".xml"));
            Debug.Log(report.ToString());
        }

        //按建筑配置采集蓝图和斜视图标。
        private static XElement Capture(XElement definition, string projectRoot, AssetBundle bundle, bool exportMenu)
        {
            bool silo = definition.Element("thingClass").Value == "SSR.Combat.Offscreen.Building_MissileSilo";
            var prefabSettings = silo ? null : definition.Element("comps").Elements("li")
                .Single(element => element.Attribute("Class")?.Value == "SSR.Combat.Offscreen.OffscreenPrefabProperties");
            var aimSettings = silo ? null : definition.Element("modExtensions").Elements("li")
                .Single(element => element.Attribute("Class")?.Value == "SSR.Combat.Offscreen.TurretAimSettings");
            var siloSettings = silo ? definition.Element("modExtensions").Elements("li")
                .Single(element => element.Attribute("Class")?.Value == "SSR.Combat.Offscreen.SiloSettings") : null;
            string prefabPath = silo ? siloSettings.Element("prefabPath").Value
                : prefabSettings.Element("leveledPrefabPaths").Element("li").Value;
            var prefab = bundle.LoadAsset<GameObject>(prefabPath);
            if (!prefab) throw new InvalidOperationException("炮塔预制体不存在：" + prefabPath);
            ValidateNormals(prefab);
            var model = UnityEngine.Object.Instantiate(prefab);
            var cameraObject = new GameObject("炮塔图标离屏相机");
            var output = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
            Texture2D texture = null;
            var previous = RenderTexture.active;
            try
            {
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var size = ReadVector(definition.Element("graphicData").Element("drawSize").Value);
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = silo ? Vector3.one : new Vector3(size.x, 1, size.y);
                if (!silo)
                {
                    var root = model.transform.Find(prefabSettings.Element("modelRootPath").Value);
                    if (!root) throw new InvalidOperationException("炮塔模型根节点不存在：" + prefabPath);
                    root.localRotation = Quaternion.identity;
                    ApplyIdle(model, aimSettings, "yawPath", "yawRotationAixe", "idleYaw");
                    ApplyIdle(model, aimSettings, "pitchPath", "pitchRotationAixe", "idlePitch");
                }
                else
                {
                    string layoutPath = siloSettings.Element("layoutPath").Value;
                    var layout = SiloLayoutReader.Read(bundle.LoadAsset<TextAsset>(layoutPath).text, layoutPath);
                    foreach (var slot in layout.slots)
                    {
                        bundle.LoadAsset<AnimationClip>(slot.openingPath).SampleAnimation(model.transform.Find(layout.animatorPath).gameObject, 0);
                        model.transform.Find(slot.path).gameObject.SetActive(false);
                    }
                    var properties = new MaterialPropertyBlock();
                    properties.SetFloat("_ClipGround", 1);
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.SetPropertyBlock(properties);
                }
                cameraObject.AddComponent<Camera>();
                var capture = cameraObject.AddComponent<TurretOutlineCapture>();
                capture.continuous = false;
                capture.target = model.transform;
                capture.output = output;
                capture.geometryShader = bundle.LoadAsset<Shader>("Assets/SSR/Outline/Shaders/TurretGeometry.shader");
                capture.compositeShader = bundle.LoadAsset<Shader>("Assets/SSR/Outline/Shaders/TurretOutline.shader");
                capture.orthographicProjection = true;
                capture.supersampling = 2;
                capture.silhouetteWidth = 3.7f;
                capture.structureWidth = 0;
                capture.surfaceLighting = true;
                texture = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                var camera = cameraObject.GetComponent<Camera>();
                var framing = new XElement("Turret", new XAttribute("defName", definition.Element("defName").Value));
                var blueprint = definition.Element("building").Element("blueprintGraphicData");
                string texturePath = blueprint.Element("texPath").Value;
                if (silo)
                {
                    texturePath = "Things/Building/Security/MissileSilo/MissileSilo_Blueprint";
                    SetValue(blueprint, "texPath", texturePath);
                    SetValue(blueprint, "graphicClass", "Graphic_Multi");
                    SetValue(blueprint, "drawRotated", "false");
                }
                string[] directions = { "North", "East", "South", "West" };
                for (int direction = 0; direction < (silo ? 4 : 1); direction++)
                {
                    if (silo) model.transform.rotation = Quaternion.Euler(0,
                        direction * 90 + (float)siloSettings.Element("modelYawOffset"), 0);
                    cameraObject.transform.rotation = Quaternion.Euler(60, 0, 0);
                    capture.FrameTarget();
                    SaveImage(capture, texture, projectRoot, texturePath + (silo ? "_" + directions[direction].ToLowerInvariant() : ""));
                    //按当前建筑朝向计算蓝图尺寸和偏移。
                    Vector3 anchor = new Vector3(0, silo ? 0 : FloorHeight(model), 0);
                    Vector3 delta = camera.transform.position - anchor;
                    float mapSize = camera.orthographicSize * 2;
                    string sizeValue = "(" + Number(mapSize) + "," + Number(mapSize) + ")";
                    string offsetField = "drawOffset" + (silo ? directions[direction] : "");
                    string offsetValue = "(" + Number(Vector3.Dot(delta, camera.transform.right)) + ",0,"
                        + Number(Vector3.Dot(delta, camera.transform.up)) + ")";
                    SetValue(blueprint, "drawSize", sizeValue);
                    SetValue(blueprint, offsetField, offsetValue);
                    framing.Add(new XElement("View", new XAttribute("direction", directions[direction]),
                        new XElement("drawSize", sizeValue), new XElement("drawOffset", offsetValue)));
                }
                if (!exportMenu) return framing;
                if (!silo)
                {
                    var muzzle = model.transform.Find(aimSettings.Element("shootingOrigine").Value);
                    if (!muzzle) throw new InvalidOperationException("炮塔开火点不存在：" + prefabPath);
                    Vector3 forward = muzzle.TransformVector(ReadVector(aimSettings.Element("shootingOrigineForward").Value));
                    float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                    model.transform.rotation = Quaternion.Euler(0, 180 - yaw, 0);
                }
                else model.transform.rotation = Quaternion.Euler(0, 180, 0);
                cameraObject.transform.rotation = Quaternion.Euler(55, 55, 0);
                capture.FrameTarget();
                camera.orthographicSize *= 0.92f;
                SaveImage(capture, texture, projectRoot, definition.Element("uiIconPath").Value);
                return framing;
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

        //检查资源包是否保留实体网格的光照法线。
        private static void ValidateNormals(GameObject prefab)
        {
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.GetComponent<Renderer>().sharedMaterials.Any(material => material && material.shader.name == "Unlit/ColorOnly")) continue;
                if (!filter.sharedMesh.HasVertexAttribute(VertexAttribute.Normal))
                    throw new InvalidOperationException(prefab.name + " 缺少网格法线：" + filter.name);
            }
        }

        //更新 XML 字段并保留同级缩进。
        private static void SetValue(XElement parent, string name, string value)
        {
            var existing = parent.Element(name);
            if (existing != null) { existing.Value = value; return; }
            var last = parent.Elements().Last();
            last.AddAfterSelf(new XText((last.PreviousNode as XText)?.Value ?? Environment.NewLine), new XElement(name, value));
        }

        //将当前取景保存为透明 PNG。
        private static void SaveImage(TurretOutlineCapture capture, Texture2D texture, string projectRoot, string texturePath)
        {
            capture.RenderNow();
            RenderTexture.active = capture.output;
            texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            texture.Apply();
            string destination = Path.Combine(projectRoot, "1.6/Textures", texturePath + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllBytes(destination, texture.EncodeToPNG());
        }

        //计算实体模型底面高度。
        private static float FloorHeight(GameObject model)
        {
            return model.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled
                && !renderer.sharedMaterials.Any(material => material && material.shader && material.shader.name.Contains("Wireframe")))
                .Min(renderer => renderer.bounds.min.y);
        }

        //以区域无关的小数格式输出 XML 数值。
        private static string Number(float value) => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        //在关节初始旋转上叠加配置的默认角度。
        private static void ApplyIdle(GameObject model, XElement settings, string pathField, string axisField, string angleField)
        {
            var joint = model.transform.Find(settings.Element(pathField).Value);
            if (!joint) throw new InvalidOperationException("炮塔关节不存在：" + settings.Element(pathField).Value);
            float angle = (float?)settings.Element(angleField) ?? 0;
            joint.localRotation *= Quaternion.AngleAxis(angle, ReadVector(settings.Element(axisField).Value));
        }

        //解析 XML 中的二维或三维向量。
        private static Vector3 ReadVector(string value)
        {
            var numbers = value.Trim('(', ')').Split(',')
                .Select(number => float.Parse(number, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new Vector3(numbers[0], numbers[1], numbers.Length == 3 ? numbers[2] : 0);
        }
    }
}
