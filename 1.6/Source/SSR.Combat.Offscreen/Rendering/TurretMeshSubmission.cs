using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSR.Combat.Offscreen
{
    //缓存炮塔网格，通过远处的变换矩阵绘制，逻辑挂点始终留在地图坐标中。
    internal sealed class TurretMeshSubmission : IDisposable
    {
        private readonly List<MeshPart> parts = new List<MeshPart>();
        internal readonly TurretCaptureFrame Frame;
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        internal bool ClipGround;

        //记录单个网格及其材质，蒙皮网格保留独立的烘焙缓存。
        private sealed class MeshPart
        {
            internal Renderer Renderer;
            internal Mesh Mesh;
            internal SkinnedMeshRenderer Skin;
            internal Material[] Materials;
            internal bool RotatingSurface;
        }

        //屏蔽模型的自动渲染，只收集原先启用且不属于旧描边的网格。
        internal TurretMeshSubmission(GameObject root, Transform rotor = null)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.forceRenderingOff = true;
                if (!renderer.enabled) continue;
                var materials = renderer.sharedMaterials;
                if (Array.Exists(materials, m => m && m.shader && m.shader.name.Contains("Wireframe"))) continue;
                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = skin ? new Mesh { name = "炮塔蒙皮缓存" } : filter ? filter.sharedMesh : null;
                if (!mesh) throw new NotSupportedException("炮塔离屏网格类型不受支持：" + renderer.name);
                parts.Add(new MeshPart
                {
                    Renderer = renderer, Mesh = mesh, Skin = skin, Materials = materials,
                    RotatingSurface = rotor && (renderer.transform == rotor || renderer.transform.IsChildOf(rotor))
                });
            }
            if (parts.Count == 0) throw new InvalidOperationException("炮塔预制体没有启用的实体网格：" + root.name);
            var bounds = parts[0].Renderer.bounds;
            foreach (var part in parts) bounds.Encapsulate(part.Renderer.bounds);
            Frame = new TurretCaptureFrame(root.transform, bounds);
        }

        //用同一套动画变换分别提交颜色和几何信息，避免两遍采集产生错位。
        internal bool Draw(CommandBuffer color, CommandBuffer geometry, Material geometryMaterial, Matrix4x4 offset, out Bounds bounds)
        {
            properties.SetFloat("_ClipGround", ClipGround ? 1 : 0);
            bounds = default;
            bool found = false;
            foreach (var part in parts)
            {
                if (!part.Renderer || !part.Renderer.enabled || !part.Renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(part.Renderer.bounds);
                else { bounds = part.Renderer.bounds; found = true; }
                if (part.Skin) part.Skin.BakeMesh(part.Mesh);
                var matrix = offset * part.Renderer.localToWorldMatrix;
                //只标记转轮及其子网格，独立导弹和炮身不参与旋转照明平均。
                properties.SetFloat("_RotatingSurface", part.RotatingSurface ? 1 : 0);
                for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                {
                    var material = part.Materials[Math.Min(sub, part.Materials.Length - 1)];
                    if (!material) continue;
                    color.DrawMesh(part.Mesh, matrix, material, sub, 0, properties);
                    geometry.DrawMesh(part.Mesh, matrix, geometryMaterial, sub, 0, properties);
                }
            }
            return found;
        }

        //向透明尾迹通道只写入实体深度，烟焰不会穿过井盖或弹体。
        internal void DrawDepth(CommandBuffer commands, Material material, Matrix4x4 offset)
        {
            properties.SetFloat("_ClipGround", ClipGround ? 1 : 0);
            foreach (var part in parts)
            {
                if (!part.Renderer || !part.Renderer.enabled || !part.Renderer.gameObject.activeInHierarchy) continue;
                if (part.Skin) part.Skin.BakeMesh(part.Mesh);
                for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                    commands.DrawMesh(part.Mesh, offset * part.Renderer.localToWorldMatrix, material, sub, 0, properties);
            }
        }

        //释放本组件创建的蒙皮缓存，静态共享网格仍由资源包管理。
        public void Dispose()
        {
            Frame.Dispose();
            foreach (var part in parts) if (part.Skin && part.Mesh) UnityEngine.Object.Destroy(part.Mesh);
            parts.Clear();
        }
    }
}
