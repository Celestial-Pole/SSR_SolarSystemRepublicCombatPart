using System;
using System.Collections.Generic;
using Waylith.Rendering;
namespace SSR.Combat.Offscreen;

//从 GLSL 创建原生 Shader，资源名称仅用于识别模型材质。
internal static class SsrShaders
{
    private static readonly Dictionary<string, Waylith.Rendering.Shader> shaders = new(StringComparer.Ordinal);
    //实体光影由实时阶段负责，这里保留模型原色和独立透明烟焰通道。
    internal static void Initialize(string directory)
    {
        ShaderSourceLoader.RegisterShaderSourceDirectory(directory);
        Add("Unlit/ColorOnly", "Color");
        Add("SSR/Missiles/Occluder", "Occluder", colorWrite: false);
        Add("SSR/Missiles/Exhaust", "Exhaust", blend: true, depthWrite: false);
        Add("SSR/Missiles/Map", "TransparentMap", blend: true);
    }
    //必需材质缺失时报告具体名称。
    internal static Waylith.Rendering.Shader Require(string name) => shaders.TryGetValue(name, out var shader)
        ? shader : throw new InvalidOperationException("SSR 原生材质类型不存在：" + name);
    //混合、深度和顶点布局沿用相同的透明预乘公式。
    private static void Add(string name, string pass, bool blend = false, bool depthWrite = true, bool colorWrite = true)
    {
        var definition = new ShaderProgramDefinition
        {
            vertexShaderFileName = "SSR_Model.vert.glsl", fragmentShaderFileName = "SSR_" + pass + ".frag.glsl",
            flipTextureST = false, cullMode = FaceCull.None, depthTest = true,
            depthWrite = depthWrite, colorWrite = colorWrite, transparent = blend,
            blendMode = blend ? ShaderBlendMode.Custom : ShaderBlendMode.Opaque,
            blendSrcFactor = BlendFactor.One, blendDstFactor = BlendFactor.OneMinusSrcAlpha,
            renderQueue = new RenderQueueValue(blend ? 3000 : 2000)
        };
        var shader = new Waylith.Rendering.Shader("SSR/Waylith/" + pass, "SSR/Waylith/" + pass, definition);
        if (shader.HasErrors) throw new InvalidOperationException("SSR Shader 接口或参数布局无效：" + pass);
        ShaderRegistry.Register(shader);
        shaders.Add(name, shader);
    }
}
