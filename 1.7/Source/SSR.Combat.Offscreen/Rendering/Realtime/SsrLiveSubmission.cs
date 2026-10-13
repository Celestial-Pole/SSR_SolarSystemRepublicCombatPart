using System;
using System.Collections.Generic;
using System.Numerics;
using Waylith.Graphics.Meshes;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//保留最终地图位置及采集姿态，所有可见网格统一写入共享屏幕附件。
internal sealed class SsrLiveSubmission(SsrLiveEntity entity, Mesh quad, Matrix4x4 matrix,
    UnityEngine.Vector3 center, CameraView view, DrawProperty[] properties, int id)
{
    internal readonly Mesh Quad = quad;
    internal readonly Matrix4x4 Matrix = matrix;
    internal readonly UnityEngine.Vector3 Center = center;
    internal readonly CameraView View = view;
    internal readonly DrawProperty[] Properties = properties;

    //实体内部保留真实深度，整座实体之间沿用地图面片的原排序。
    internal void AddModels(List<FinalDrawItem>[] lists, Material[] materials, DrawPropertyArena arena,
        DrawItemFactory factory, float depthBase, float depthSpan)
    {
        foreach (var draw in entity.Draws)
        {
            DrawProperty[] values =
            {
                DrawProperty.Matrix(ShaderPropertyId.GetOrCreate("_SSRView"), UnityEngine.Matrix4x4.ToNumerics(entity.View)),
                DrawProperty.Vector(ShaderPropertyId.GetOrCreate("_SSRMapCenter"), new Vector4(Center.x, Center.y, Center.z, 0)),
                DrawProperty.Vector(ShaderPropertyId.GetOrCreate("_SSRDepth"), new Vector4(depthBase, depthSpan, entity.Near, entity.Far)),
                DrawProperty.Vector(ShaderPropertyId.GetOrCreate("_Color"), draw.Color),
                DrawProperty.Float(ShaderPropertyId.GetOrCreate("_ClipGround"), draw.ClipGround),
                DrawProperty.Float(ShaderPropertyId.GetOrCreate("_RotatingSurface"), draw.RotatingSurface),
                DrawProperty.Float(ShaderPropertyId.GetOrCreate("_SSRObjectId"), id)
            };
            var range = arena.Copy(values);
            for (int pass = 0; pass < 2; pass++)
            {
                var command = new MeshDrawCommand(draw.Mesh, materials[pass], range, draw.Matrix, draw.Submesh);
                lists[pass].Add(factory.TryMake(command, arena, UnityEngine.Shader.GlobalKeywordKey,
                    diagnosticPrefix: "SSR实时") ?? throw new InvalidOperationException("SSR 实时模型命令解析失败。"));
            }
        }
    }
}
