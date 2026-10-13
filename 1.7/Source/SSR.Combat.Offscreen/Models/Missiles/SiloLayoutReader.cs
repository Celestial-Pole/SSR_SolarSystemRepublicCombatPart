using System;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace SSR.UnityComponent.Missiles
{
    //通过托管 JSON 读取器解析九舱配置，不依赖 Unity 对外部程序集的类型序列化。
    public static class SiloLayoutReader
    {
        //读取完整布局并检查必需字段，错误信息包含配置资源路径。
        public static SiloLayoutData Read(string json, string resourcePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) throw new FormatException("配置内容为空。");
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json), XmlDictionaryReaderQuotas.Max))
                {
                    var root = XElement.Load(reader);
                    var layout = new SiloLayoutData
                    {
                        animatorPath = ReadText(root, "animatorPath"),
                        openingSeconds = ReadNumber(root, "openingSeconds"),
                        clearanceHeight = ReadNumber(root, "clearanceHeight"),
                        captureCenter = ReadVector(root, "captureCenter"),
                        captureSize = ReadVector(root, "captureSize"),
                        slots = Require(root, "slots").Elements("item").Select(ReadSlot).ToArray()
                    };
                    if (layout.slots.Length != 9 || layout.openingSeconds <= 0 || layout.clearanceHeight < 0
                        || !Positive(layout.captureSize))
                        throw new FormatException("配置需要九个舱位、正数开盖时长和有效取景范围。");
                    return layout;
                }
            }
            catch (Exception error) when (error is FormatException || error is XmlException)
            {
                throw new InvalidOperationException("导弹井布局解析失败：" + resourcePath + "；" + error.Message, error);
            }
        }

        //读取一个舱位的姿态、缩放和头尾挂点，拒绝零尺寸及无效朝向。
        private static SiloSlotData ReadSlot(XElement node)
        {
            var rotation = Require(node, "rotation");
            var slot = new SiloSlotData
            {
                path = ReadText(node, "path"),
                openingPath = ReadText(node, "openingPath"),
                openingSeconds = ReadNumber(node, "openingSeconds"),
                position = ReadVector(node, "position"),
                rotation = new Quaternion(ReadNumber(rotation, "x"), ReadNumber(rotation, "y"),
                    ReadNumber(rotation, "z"), ReadNumber(rotation, "w")),
                scale = ReadVector(node, "scale"),
                tip = ReadVector(node, "tip"),
                trail = ReadVector(node, "trail"),
                tailZ = ReadNumber(node, "tailZ")
            };
            if (slot.openingSeconds <= 0 || !Positive(slot.scale) || Mathf.Abs(Quaternion.Dot(slot.rotation, slot.rotation) - 1f) > 0.001f
                || slot.tip.z <= slot.trail.z || slot.tailZ > slot.trail.z)
                throw new FormatException("舱位姿态或头尾挂点无效：" + slot.path);
            return slot;
        }

        //获取必需字段，拒绝缺失及显式空值，避免后续访问产生空引用。
        private static XElement Require(XElement parent, string name)
        {
            var node = parent.Element(name);
            if (node == null || (string)node.Attribute("type") == "null")
                throw new FormatException("缺少字段：" + parent.Name + "/" + name);
            return node;
        }

        //读取非空资源节点路径。
        private static string ReadText(XElement parent, string name)
        {
            string text = Require(parent, name).Value;
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("字段为空：" + name);
            return text;
        }

        //按固定文化读取有限浮点数，保留 JSON 中的小数及科学计数法。
        private static float ReadNumber(XElement parent, string name)
        {
            if (!float.TryParse(Require(parent, name).Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("字段不是有效数值：" + parent.Name + "/" + name);
            return value;
        }

        //明确读取三维向量的三个分量，避免反射处理 Unity 的计算属性。
        private static Vector3 ReadVector(XElement parent, string name)
        {
            var node = Require(parent, name);
            return new Vector3(ReadNumber(node, "x"), ReadNumber(node, "y"), ReadNumber(node, "z"));
        }

        private static bool Positive(Vector3 vector)
        {
            return vector.x > 0 && vector.y > 0 && vector.z > 0;
        }
    }
}
