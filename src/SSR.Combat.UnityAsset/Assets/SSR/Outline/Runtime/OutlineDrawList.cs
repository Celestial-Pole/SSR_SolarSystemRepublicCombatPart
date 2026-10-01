using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SSR.UnityComponent.Outline
{
    //建立目标炮塔的绘制列表，排除旧线框和其他场景物体。
    internal sealed class OutlineDrawList
    {
        private readonly List<Renderer> renderers = new List<Renderer>();

        //收集指定根节点下的网格渲染器。
        internal void Collect(Transform root)
        {
            renderers.Clear();
            root.GetComponentsInChildren(false, renderers);
        }

        //识别只有旧描边材质的重复网格。
        internal static bool IsLegacy(Material material)
        {
            return material != null && material.shader != null && material.shader.name == "Unlit/Wireframe";
        }

        //按真实子网格构造颜色和几何绘制命令，并返回可见模型边界。
        internal Bounds Build(CommandBuffer color, CommandBuffer geometry, Material geometryMaterial)
        {
            color.Clear();
            geometry.Clear();
            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var material = materials[slot];
                    if (material == null || IsLegacy(material)) continue;
                    int submesh = Mathf.Min(slot, mesh.subMeshCount - 1);
                    color.DrawRenderer(renderer, material, submesh, 0);
                    geometry.DrawRenderer(renderer, geometryMaterial, submesh, 0);
                    if (found) bounds.Encapsulate(renderer.bounds);
                    else { bounds = renderer.bounds; found = true; }
                }
            }
            if (!found) throw new InvalidOperationException("描边目标没有启用的实体网格。");
            return bounds;
        }
    }
}
