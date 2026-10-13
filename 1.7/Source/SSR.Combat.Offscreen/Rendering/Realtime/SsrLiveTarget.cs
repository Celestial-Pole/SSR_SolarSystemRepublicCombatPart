using System;
using Brutal.VulkanApi;
using Waylith.Rendering;
using NativeTarget = Waylith.Rendering.RenderTexture;

namespace SSR.Combat.Offscreen;

//共享屏幕附件由引擎重建事件统一调整，采样包装不拥有底层图像。
internal sealed class SsrLiveTarget : Texture, IDisposable
{
    internal readonly NativeTarget Target;
    public override int width { get => Target.Width; set => throw new NotSupportedException(); }
    public override int height { get => Target.Height; set => throw new NotSupportedException(); }

    //颜色保存对象编号，几何保存位置和法线；阴影使用单通道浮点。
    internal SsrLiveTarget(int width, int height, VkFormat format, bool depth, string name, bool linear = false)
    {
        Target = new NativeTarget(width, height, format, depth ? VkFormat.D32SFloat : VkFormat.Undefined,
            linear ? RenderTextureFilter.Bilinear : RenderTextureFilter.Point, name: name);
        Name = name;
        filterMode = linear ? FilterMode.Bilinear : FilterMode.Point;
        wrapMode = TextureWrapMode.Clamp;
        Target.Create();
        GpuResource = Target.Color;
    }

    //窗口变化时由引擎在安全重建阶段释放旧附件。
    internal void Resize(int width, int height)
    {
        Target.Resize(width, height);
        GpuResource = Target.Color;
    }

    //仅由目标释放图像，避免采样包装重复销毁同一 Vulkan 资源。
    public override void Dispose()
    {
        GpuResource = null;
        Target.Dispose();
    }
}
