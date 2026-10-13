using System;
using System.Collections.Generic;
using System.Numerics;
using Brutal.VulkanApi;
using Waylith.Graphics;
using Waylith.Rendering;
using Waylith.Rendering.PostFx;
using UBlock = SSR.Combat.Offscreen.SsrDrawProperties;
using UMatrix = UnityEngine.Matrix4x4;

namespace SSR.Combat.Offscreen;

//在地图之前录制共享屏幕几何与阴影，同帧地图合成直接读取结果。
internal sealed class SsrLiveFeature : IRenderFeature, IRenderPass
{
    private readonly IRenderContext context;
    private readonly IRenderPass[] passes;
    private readonly Material[] materials;
    private readonly List<SsrLiveSubmission> pending = new();
    private readonly List<SsrEffectsFrame> effects = new();
    //透明命令与实体使用同一个帧生命周期。
    internal void AddEffects(SsrEffectsFrame effect) => effects.Add(effect);
    private readonly SsrLiveTarget[] targets = new SsrLiveTarget[4];
    private readonly Dictionary<(VkFormat, VkFormat), VkRenderPass> renderPasses = new();
    private MeshBatchRenderer renderer;
    private DrawItemFactory factory;
    private bool disposed;
    internal Material Composite => materials[4];
    public bool IsActive { get; private set; }
    public IReadOnlyList<IRenderPass> Passes => passes;
    public byte PassId => 0;
    public PassAttachment Attachment => null;
    public bool PreBackbuffer => true;

    //仅注册材质，屏幕附件按需建立且不随镜头缩放重建。
    internal SsrLiveFeature(IRenderContext context)
    {
        this.context = context;
        passes = new IRenderPass[] { this };
        string[] names = { "LiveColor", "LiveGeometry", "LiveContact", "LiveFilter", "LiveComposite" };
        materials = new Material[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var definition = new ShaderProgramDefinition
            {
                vertexShaderFileName = i < 2 ? "SSR_LiveModel.vert.glsl" : "SSR_Model.vert.glsl",
                fragmentShaderFileName = "SSR_" + names[i] + ".frag.glsl",
                cullMode = FaceCull.None, depthTest = i < 2 || i == 4, depthWrite = i < 2 || i == 4,
                transparent = i == 4, blendMode = i == 4 ? ShaderBlendMode.Custom : ShaderBlendMode.Opaque,
                blendSrcFactor = BlendFactor.One, blendDstFactor = BlendFactor.OneMinusSrcAlpha,
                flipTextureST = false
            };
            var shader = new Shader("SSR/" + names[i], "SSR/" + names[i], definition);
            if (shader.HasErrors) throw new InvalidOperationException("SSR 实时 Shader 接口错误：" + names[i]);
            ShaderRegistry.Register(shader);
            materials[i] = new Material(shader, "SSR/" + names[i]);
        }
    }

    //每座实体只传递值快照，共享屏幕的对象编号隔离阴影和轮廓采样。
    internal int Add(SsrLiveEntity entity, Waylith.Graphics.Meshes.Mesh quad, Matrix4x4 matrix,
        UnityEngine.Vector3 center, CameraView view, UBlock properties)
    {
        EnsureTargets();
        int id = pending.Count + 1;
        properties.SetFloat("_SSRObjectId", id);
        var inverse = properties.GetMatrix("_SSRCaptureFromScreen");
        var texel = new UnityEngine.Vector2(
            Math.Abs(inverse.m00) / targets[0].width + Math.Abs(inverse.m01) / targets[0].height,
            Math.Abs(inverse.m10) / targets[0].width + Math.Abs(inverse.m11) / targets[0].height);
        properties.SetVector("_SSRGeometryTexelSize", new UnityEngine.Vector4(texel.x, texel.y, 1 / texel.x, 1 / texel.y));
        var values = new DrawProperty[properties.PropertyCount];
        properties.CopyTo(values);
        pending.Add(new SsrLiveSubmission(entity, quad, matrix, center, view, values, id));
        return id;
    }

    //合成命令保留当前附件引用，原有 IMGUI 和地图光照仍按原版顺序执行。
    internal DrawPropertyBlock ComposeProperties(UBlock properties)
    {
        var values = new DrawProperty[properties.PropertyCount];
        properties.CopyTo(values);
        return new DrawPropertyBlock(BindTextures(values, targets[0], Vector4.Zero));
    }

    //所有纹理输入显式绑定，不依赖跨线程变化的全局材质参数。
    private DrawProperty[] BindTextures(DrawProperty[] values, Texture main, Vector4 blur)
    {
        var result = new DrawProperty[values.Length + 6];
        values.CopyTo(result, 0);
        int i = values.Length;
        result[i++] = DrawProperty.Texture(ShaderPropertyId.GetOrCreate("_MainTex"), main);
        result[i++] = DrawProperty.Texture(ShaderPropertyId.GetOrCreate("_GeometryTex"), targets[1]);
        result[i++] = DrawProperty.Texture(ShaderPropertyId.GetOrCreate("_ContactVisibilityTex"), targets[2]);
        result[i++] = DrawProperty.Texture(ShaderPropertyId.GetOrCreate("_SSRIdTex"), targets[0]);
        result[i++] = DrawProperty.Vector(ShaderPropertyId.GetOrCreate("_SSRScreenTexelSize"),
            new Vector4(1f / targets[0].width, 1f / targets[0].height, targets[0].width, targets[0].height));
        result[i] = DrawProperty.Vector(ShaderPropertyId.GetOrCreate("_ContactBlurDirection"), blur);
        return result;
    }

    //主线程解析独立属性区，渲染线程只接收已经冻结的网格命令。
    public object CaptureFrame(in FrameTiming timing)
    {
        OffscreenTurretRenderer.CollectFrame();
        IsActive = pending.Count != 0 || effects.Count != 0;
        if (!IsActive) return null;
        GpuTextureService.FlushPendingTextureCommands();
        context.GraphicsDevice.MeshUploads.DrainPendingUploads();
        factory ??= new DrawItemFactory(context.GraphicsDevice);
        var arena = new DrawPropertyArena();
        var lists = new List<FinalDrawItem>[5];
        for (int i = 0; i < lists.Length; i++) lists[i] = new List<FinalDrawItem>();
        pending.Sort((a, b) => a.Center.y.CompareTo(b.Center.y));
        var payload = new SsrLivePayload(timing.FrameNumber, pending.Count != 0 ? pending[0].View : effects[0].View, lists, effects.ToArray());
        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var item = pending[i];
                item.AddModels(lists, materials, arena, factory, (i + 0.1f) / pending.Count, 0.8f / pending.Count);
                for (int pass = 2; pass < 5; pass++)
                {
                    var main = targets[pass == 2 ? 0 : pass == 3 ? 2 : 3];
                    var blur = pass == 3 ? new Vector4(1, 0, 0, 0) : new Vector4(0, 1, 0, 0);
                    var range = arena.Copy(BindTextures(item.Properties, main, blur));
                    var command = new MeshDrawCommand(item.Quad, materials[pass == 2 ? 2 : 3], range, item.Matrix, 0);
                    lists[pass].Add(factory.TryMake(command, arena, UnityEngine.Shader.GlobalKeywordKey,
                        diagnosticPrefix: "SSR实时") ?? throw new InvalidOperationException("SSR 实时阴影命令解析失败。"));
                }
            }
            foreach (var effect in effects) effect.Resolve(factory, arena);
            return payload;
        }
        catch { payload.Dispose(); throw; }
        finally { pending.Clear(); effects.Clear(); }
    }

    //所有步骤使用引擎提供的当前帧命令缓冲，不进行同步提交或 GPU 等待。
    public void Record(in FrameContext ctx, object featurePayload)
    {
        if (featurePayload is not SsrLivePayload payload) return;
        renderer ??= new MeshBatchRenderer(context.GraphicsDevice, context);
        renderer.BeginFrame(ctx, payload.FrameNumber);
        for (int i = 0; payload.Draws[0].Count != 0 && i < 5; i++)
        {
            var target = targets[i < 3 ? i : i == 3 ? 3 : 2].Target;
            RecordTarget(ctx, target, payload.Draws[i], payload.View, i >= 2);
        }
        foreach (var effect in payload.Effects)
            RecordTarget(ctx, effect.Target.Target, effect.Draws, effect.View, false);
    }

    //所有采集阶段使用原生附件和当前帧命令缓冲。
    private void RecordTarget(in FrameContext ctx, RenderTexture target, IReadOnlyList<FinalDrawItem> draws, CameraView view, bool shadow)
    {
        var key = (target.ColorFormat, target.DepthFormat);
        if (!renderPasses.TryGetValue(key, out var pass))
            renderPasses.Add(key, pass = PostFxPassHelpers.CreateColorRenderPass(ctx.Device,
                key.ColorFormat, key.DepthFormat, VkAttachmentLoadOp.Clear));
        var frame = new FrameContext(ctx.RenderContext, ctx.Resources, ctx.CommandBuffer, pass,
            target.GetFramebuffer(pass), target.Extent, VkSampleCountFlags._1Bit, target.ColorFormat, target.DepthFormat);
        SsrLivePass.Record(frame, target, renderer, draws, view, shadow);
    }

    //两倍屏幕采样负责亚像素边缘，附件尺寸只由窗口决定。
    private void EnsureTargets()
    {
        if (targets[0] != null) return;
        var extent = context.GetExtent();
        int width = extent.Width * 2, height = extent.Height * 2;
        for (int i = 0; i < targets.Length; i++)
            targets[i] = new SsrLiveTarget(width, height, i < 2 ? VkFormat.R32G32B32A32SFloat : VkFormat.R16SFloat,
                i < 2, "SSR共享屏幕" + i);
    }

    //引擎已等待在途帧后再触发窗口附件重建。
    public void OnRenderTargetRebuilt(IRenderTarget target)
    {
        if (target is not IRenderContext renderContext) return;
        renderer?.OnRenderContextRebuilt(renderContext);
        if (targets[0] == null) return;
        var extent = renderContext.GetExtent();
        foreach (var texture in targets) texture.Resize(extent.Width * 2, extent.Height * 2);
    }

    //由帧渲染器在退出时释放共享资源，不与地图对象的销毁交叉。
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var effect in effects) effect.Dispose();
        effects.Clear();
        renderer?.Dispose();
        factory?.Dispose();
        foreach (var target in targets) target?.Dispose();
        foreach (var pass in renderPasses.Values) context.Device.DestroyRenderPass(pass, context.Device.VkAllocator);
        foreach (var material in materials) material.Dispose();
        SsrModelLibrary.Release();
    }
}
