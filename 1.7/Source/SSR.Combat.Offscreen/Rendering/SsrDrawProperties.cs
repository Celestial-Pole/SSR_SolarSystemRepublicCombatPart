using System.Collections.Generic;
using UnityEngine;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//构建一次绘制的原生属性快照，仅接收现有炮塔着色参数。
internal sealed class SsrDrawProperties
{
    private readonly Dictionary<int, DrawProperty> values = new();
    internal int PropertyCount => values.Count;
    //开始下一次网格提交。
    internal void Clear() => values.Clear();
    //保持浮点值与原 Shader 属性名称一致。
    internal void SetFloat(string name, float value) => values[ShaderPropertyId.GetOrCreate(name)] = DrawProperty.Float(ShaderPropertyId.GetOrCreate(name), value);
    //读取实体地面裁剪和转轮标记。
    internal float GetFloat(string name) => values.TryGetValue(ShaderPropertyId.GetOrCreate(name), out var value) ? value.FloatValue : 0;
    //向量转换只发生在原版数学类型与原生绘制接口之间。
    internal void SetVector(string name, Vector4 value) => values[ShaderPropertyId.GetOrCreate(name)] = DrawProperty.Vector(ShaderPropertyId.GetOrCreate(name), new System.Numerics.Vector4(value.x, value.y, value.z, value.w));
    //颜色保持原始 RGBA，不执行色彩空间转换。
    internal void SetColor(string name, Color value) => SetVector(name, new Vector4(value.r, value.g, value.b, value.a));
    //矩阵按引擎要求使用相同的数值布局。
    internal void SetMatrix(int id, Matrix4x4 value) => values[id] = DrawProperty.Matrix(id, Matrix4x4.ToNumerics(value));
    //读取屏幕到取景坐标的反变换。
    internal Matrix4x4 GetMatrix(string name) => ToUnity(values[ShaderPropertyId.GetOrCreate(name)].MatrixValue);
    //将纹理直接绑定到 Waylith 图像。
    internal void SetTexture(string name, Waylith.Rendering.Texture value) => values[ShaderPropertyId.GetOrCreate(name)] = DrawProperty.Texture(ShaderPropertyId.GetOrCreate(name), value);
    //提交后调用者可以复用构建器，已提交参数不会改变。
    internal void CopyTo(DrawProperty[] destination) => values.Values.CopyTo(destination, 0);
    //按行复制数值布局，不改变矩阵约定。
    private static Matrix4x4 ToUnity(System.Numerics.Matrix4x4 n) => new Matrix4x4(
        new Vector4(n.M11,n.M21,n.M31,n.M41), new Vector4(n.M12,n.M22,n.M32,n.M42),
        new Vector4(n.M13,n.M23,n.M33,n.M43), new Vector4(n.M14,n.M24,n.M34,n.M44));
    //冻结当前属性列表。
    internal DrawProperty[] Snapshot() { var result = new DrawProperty[values.Count]; CopyTo(result); return result; }
}
