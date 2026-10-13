using System;
using System.Collections.Generic;
using UnityEngine;

namespace SSR.Combat.Offscreen;

//将纯数据节点绑定到共享原生几何，实体每帧只提交当前矩阵。
internal sealed class TurretMeshSubmission : IDisposable
{
    private readonly List<(SsrModelNode Node, SsrPrimitive Primitive, bool Rotating)> parts = new();
    private readonly SsrDrawProperties properties = new();
    internal readonly TurretCaptureFrame Frame;
    internal bool ClipGround;

    //导入阶段已经排除线框和禁用网格，不创建 Renderer 或蒙皮组件。
    internal TurretMeshSubmission(SsrModelNode root, SsrModelNode rotor = null)
    {
        foreach (var node in root.Nodes())
            foreach (var primitive in node.Primitives)
                parts.Add((node, primitive, rotor != null && node.IsChildOf(rotor)));
        if (parts.Count == 0) throw new InvalidOperationException("SSR 模型没有实体几何：" + root.name);
        //初始取景包含库存节点的原始范围，之后切换显隐不会改变模型比例。
        var bounds = parts[0].Node.WorldBounds(parts[0].Primitive);
        foreach (var part in parts) bounds.Encapsulate(part.Node.WorldBounds(part.Primitive));
        Frame = new TurretCaptureFrame(root, bounds);
    }

    //颜色直接来自 GLB 四通道数值，跳过兼容材质的转换与注册。
    internal bool Draw(CommandBuffer color, Matrix4x4 offset, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var part in parts)
        {
            if (!part.Node.activeInHierarchy) continue;
            var current = part.Node.WorldBounds(part.Primitive);
            if (found) bounds.Encapsulate(current); else { bounds = current; found = true; }
            color.CaptureDraws.Add(new SsrLiveDraw(part.Primitive.Mesh, part.Primitive.Color,
                Matrix4x4.ToNumerics(offset * part.Node.localToWorldMatrix), 0,
                ClipGround ? 1 : 0, part.Rotating ? 1 : 0));
        }
        return found;
    }

    //烟焰与弹体使用同一姿态和深度，避免尾焰穿过井盖。
    internal void DrawDepth(CommandBuffer commands, Material material, Matrix4x4 offset)
    {
        properties.SetFloat("_ClipGround", ClipGround ? 1 : 0);
        foreach (var part in parts)
            if (part.Node.activeInHierarchy)
                commands.DrawMesh(part.Primitive.Mesh, offset * part.Node.localToWorldMatrix, material, 0, 0, properties);
    }

    //实例只拥有节点和透明目标，共享静态几何由模型库持有。
    public void Dispose() { Frame.Dispose(); parts.Clear(); }
}
