using System;
using System.Collections.Generic;
using Brutal.Numerics;
using Brutal.VulkanApi;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//把实时步骤录制到引擎当前帧，跨步骤可见性由渲染通道依赖保证。
internal static class SsrLivePass
{
    //阴影背景清为一，实体背景清为零；沿用引擎反向深度的零清屏值。
    internal static unsafe void Record(in FrameContext ctx, RenderTexture target, MeshBatchRenderer renderer,
        IReadOnlyList<FinalDrawItem> draws, CameraView view, bool shadow)
    {
        VkClearValue* clear = stackalloc VkClearValue[2];
        clear[0] = new VkClearColorValue { Float32 = shadow ? new float4(1, 1, 1, 1) : new float4(0, 0, 0, 0) };
        clear[1] = new VkClearDepthStencilValue { Depth = 0, Stencil = 0 };
        ctx.CommandBuffer.BeginRenderPass(new VkRenderPassBeginInfo
        {
            RenderPass = ctx.RenderPass, Framebuffer = ctx.Framebuffer, RenderArea = new VkRect2D(ctx.Extent),
            ClearValueCount = target.HasDepth ? 2 : 1, ClearValues = clear
        }, VkSubpassContents.Inline);
        var viewport = new VkViewport
        { X = 0, Y = 0, Width = ctx.Extent.Width, Height = ctx.Extent.Height, MinDepth = 0, MaxDepth = 1 };
        var scissor = new VkRect2D(ctx.Extent);
        ctx.CommandBuffer.SetViewport(0, new ReadOnlySpan<VkViewport>(in viewport));
        ctx.CommandBuffer.SetScissor(0, new ReadOnlySpan<VkRect2D>(in scissor));
        renderer.InvalidateCommandBufferBindings();
        renderer.RecordBatch(ctx, draws, view);
        ctx.CommandBuffer.EndRenderPass();
        target.Color.SetMipLayout(0, VkImageLayout.ShaderReadOnlyOptimal);
    }
}
