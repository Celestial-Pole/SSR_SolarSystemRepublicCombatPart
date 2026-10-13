using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEngine;
using Waylith.Graphics.Meshes;
using Waylith.Rendering;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;
using NVector4 = System.Numerics.Vector4;

namespace SSR.Combat.Offscreen;

//读取独立 GLB 模型库，几何和材质共享，实例只复制可变节点。
internal sealed class SsrModelLibrary
{
    private static SsrModelLibrary instance;
    private readonly Dictionary<string, SsrModelNode> models = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int, int), Waylith.Graphics.Meshes.Mesh> geometry = new();
    private readonly List<IDisposable> owned = new();
    private readonly JsonElement document;
    private readonly byte[] binary;
    private readonly Waylith.Rendering.Material[] materials;
    private readonly NVector4[] colors;
    private readonly SsrPrimitive[][] meshes;

    //旧 XML 的路径作为稳定标识，运行时不访问对应的 Unity 资源。
    internal static SsrModelNode Require(string path)
    {
        instance ??= new SsrModelLibrary(Path.Combine(SsrCombatMod.Root, "1.7", "Models", "combat.glb"));
        return instance.models.TryGetValue(path.Replace('\\', '/'), out var model) ? model
            : throw new FileNotFoundException("SSR GLB 模型库缺少模型：" + path);
    }

    //解析 GLB 标准块，拒绝截断资源和未知容器版本。
    private SsrModelLibrary(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (reader.ReadUInt32() != 0x46546c67 || reader.ReadUInt32() != 2 || reader.ReadUInt32() != stream.Length)
            throw new InvalidDataException("SSR 模型不是完整的 GLB 2.0：" + path);
        int jsonLength = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x4e4f534a) throw new InvalidDataException("GLB 缺少 JSON 块。");
        using var json = JsonDocument.Parse(reader.ReadBytes(jsonLength));
        document = json.RootElement;
        int binaryLength = reader.ReadInt32();
        if (reader.ReadUInt32() != 0x004e4942) throw new InvalidDataException("GLB 缺少二进制块。");
        binary = reader.ReadBytes(binaryLength);
        if (binary.Length != binaryLength) throw new InvalidDataException("GLB 几何数据不完整。");
        var sourceMaterials = document.GetProperty("materials");
        materials = new Waylith.Rendering.Material[sourceMaterials.GetArrayLength()];
        colors = new NVector4[materials.Length];
        try
        {
            for (int i = 0; i < materials.Length; i++) ReadMaterial(i, sourceMaterials[i]);
            var sourceMeshes = document.GetProperty("meshes");
            meshes = new SsrPrimitive[sourceMeshes.GetArrayLength()][];
            for (int i = 0; i < meshes.Length; i++)
            {
                var primitives = sourceMeshes[i].GetProperty("primitives");
                meshes[i] = new SsrPrimitive[primitives.GetArrayLength()];
                for (int p = 0; p < meshes[i].Length; p++) meshes[i][p] = ReadPrimitive(primitives[p]);
            }
            foreach (var scene in document.GetProperty("scenes").EnumerateArray())
                models.Add(scene.GetProperty("name").GetString(), ReadNode(scene.GetProperty("nodes")[0].GetInt32()));
            Verse.Log.Message($"[SSR原生] 已加载 {models.Count} 个 GLB 模型、{geometry.Count} 份几何和 {materials.Length} 个材质。");
        }
        catch { foreach (var resource in owned) resource.Dispose(); throw; }
        //网格已复制到原生存储，原文件字节不再常驻。
        binary = Array.Empty<byte>();
        document = default;
    }

    //退出时释放共享几何与材质，不随单座建筑销毁。
    internal static void Release()
    {
        if (instance == null) return;
        foreach (var resource in instance.owned) resource.Dispose();
        instance = null;
    }

    //材质颜色和烟焰参数直接传到原生材质，不经过兼容层的属性转换。
    private void ReadMaterial(int index, JsonElement source)
    {
        var extras = source.GetProperty("extras");
        var material = new Waylith.Rendering.Material(SsrShaders.Require(extras.GetProperty("ssrShader").GetString()), source.GetProperty("name").GetString());
        owned.Add(material);
        materials[index] = material;
        var color = source.GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor");
        colors[index] = new NVector4(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle(), color[3].GetSingle());
        foreach (var property in extras.GetProperty("floats").EnumerateObject())
            material.SetFloat(ShaderPropertyId.GetOrCreate(property.Name), property.Value.GetSingle());
        foreach (var property in extras.GetProperty("colors").EnumerateObject())
        {
            var value = property.Value;
            material.SetVector(ShaderPropertyId.GetOrCreate(property.Name), new NVector4(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle(), value[3].GetSingle()));
        }
        material.SetVector(ShaderPropertyId.GetOrCreate("_Color"), colors[index]);
    }

    //GLB 右手坐标恢复成现有战斗逻辑的地图坐标，三角形绕序同步还原。
    private SsrPrimitive ReadPrimitive(JsonElement source)
    {
        var attributes = source.GetProperty("attributes");
        int position = attributes.GetProperty("POSITION").GetInt32(), indices = source.GetProperty("indices").GetInt32();
        if (!geometry.TryGetValue((position, indices), out var mesh))
        {
            var vertices = ReadVectors(position);
            var normals = attributes.TryGetProperty("NORMAL", out var normal) ? ReadVectors(normal.GetInt32()) : null;
            var triangles = ReadIndices(indices);
            foreach (int vertex in triangles)
                if ((uint)vertex >= (uint)vertices.Length) throw new InvalidDataException("SSR GLB 三角形引用了不存在的顶点。");
            for (int i = 0; i < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            mesh = new Waylith.Graphics.Meshes.Mesh(MeshArrays.Build(vertices, triangles, normals,
                uv0: attributes.TryGetProperty("TEXCOORD_0", out var uv0) ? ReadUv(uv0.GetInt32()) : null,
                uv1: attributes.TryGetProperty("TEXCOORD_1", out var uv1) ? ReadUv(uv1.GetInt32()) : null)) { DebugName = "SSR GLB " + position };
            geometry.Add((position, indices), mesh);
            owned.Add(mesh);
        }
        int material = source.GetProperty("material").GetInt32();
        return new SsrPrimitive(mesh, materials[material], colors[material]);
    }

    //保留导出层级，包括没有几何的炮口、尾喷口及动画挂点。
    private SsrModelNode ReadNode(int index)
    {
        var source = document.GetProperty("nodes")[index];
        var position = source.GetProperty("translation");
        var rotation = source.GetProperty("rotation");
        var scale = source.GetProperty("scale");
        var node = new SsrModelNode
        {
            name = source.GetProperty("name").GetString(),
            localPosition = new Vector3(position[0].GetSingle(), position[1].GetSingle(), -position[2].GetSingle()),
            localRotation = new Quaternion(-rotation[0].GetSingle(), -rotation[1].GetSingle(), rotation[2].GetSingle(), rotation[3].GetSingle()),
            localScale = new Vector3(scale[0].GetSingle(), scale[1].GetSingle(), scale[2].GetSingle())
        };
        node.SetActive(source.GetProperty("extras").GetProperty("active").GetBoolean());
        if (source.TryGetProperty("mesh", out var mesh)) node.Primitives = meshes[mesh.GetInt32()];
        if (source.TryGetProperty("children", out var children))
            foreach (var child in children.EnumerateArray()) node.AddChild(ReadNode(child.GetInt32()));
        return node;
    }

    //验证访问器的格式与边界，错误资源不得上传到显卡。
    private (int Offset, int Count, int Stride) Access(int index, string type, int component, int size)
    {
        var accessor = document.GetProperty("accessors")[index];
        if (accessor.GetProperty("type").GetString() != type || accessor.GetProperty("componentType").GetInt32() != component || accessor.TryGetProperty("sparse", out _))
            throw new NotSupportedException("SSR GLB 访问器格式不匹配：" + index);
        var view = document.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
        int offset = (view.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0)
            + (accessor.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
        int count = accessor.GetProperty("count").GetInt32();
        int stride = view.TryGetProperty("byteStride", out var s) ? s.GetInt32() : size;
        if (count < 1 || stride < size || offset < 0 || (long)offset + (long)(count - 1) * stride + size > binary.Length)
            throw new InvalidDataException("SSR GLB 访问器超出数据范围：" + index);
        return (offset, count, stride);
    }

    //读取几何向量并还原地图轴向。
    private NVector3[] ReadVectors(int index)
    {
        var a = Access(index, "VEC3", 5126, 12);
        var values = new NVector3[a.Count];
        for (int i = 0, p = a.Offset; i < values.Length; i++, p += a.Stride)
            values[i] = new NVector3(BitConverter.ToSingle(binary, p), BitConverter.ToSingle(binary, p + 4), -BitConverter.ToSingle(binary, p + 8));
        return values;
    }

    //保留烟焰网格的两组纹理坐标。
    private NVector2[] ReadUv(int index)
    {
        var a = Access(index, "VEC2", 5126, 8);
        var values = new NVector2[a.Count];
        for (int i = 0, p = a.Offset; i < values.Length; i++, p += a.Stride)
            values[i] = new NVector2(BitConverter.ToSingle(binary, p), BitConverter.ToSingle(binary, p + 4));
        return values;
    }

    //导出器使用无符号 32 位索引，运行时验证三角形完整性。
    private int[] ReadIndices(int index)
    {
        var a = Access(index, "SCALAR", 5125, 4);
        if (a.Count % 3 != 0) throw new InvalidDataException("SSR GLB 三角形索引不完整。");
        var values = new int[a.Count];
        for (int i = 0, p = a.Offset; i < values.Length; i++, p += a.Stride) values[i] = checked((int)BitConverter.ToUInt32(binary, p));
        return values;
    }
}
