using System;
using System.Threading;
using Brutal.VulkanApi;
using Waylith.Rendering;

namespace SSR.Combat.Offscreen;

//烟焰附件由显示实例和在途帧共同持有，最后一帧完成后才能销毁。
internal sealed class SsrEffectsTarget : Texture
{
    private readonly SsrLiveTarget texture;
    private int references = 1;
    private bool ownerReleased;
    internal RenderTexture Target => texture.Target;
    public override int width { get => texture.width; set => throw new NotSupportedException(); }
    public override int height { get => texture.height; set => throw new NotSupportedException(); }

    //透明烟焰保持双线性采样和独立实体深度缓冲。
    internal SsrEffectsTarget(int size)
    {
        texture = new SsrLiveTarget(size, size, VkFormat.R8G8B8A8UNorm, true, "SSR原生烟焰", true);
        Name = "SSR原生烟焰";
        filterMode = FilterMode.Bilinear;
        wrapMode = TextureWrapMode.Clamp;
        GpuResource = texture.GpuResource;
    }
    //每个冻结帧持有一个引用。
    internal void Retain() => Interlocked.Increment(ref references);
    //引擎完成该帧后归还引用。
    internal void Release()
    {
        if (Interlocked.Decrement(ref references) != 0) return;
        GpuResource = null;
        texture.Dispose();
    }
    //显示实例离开地图或换尺寸时只归还自己的引用。
    public override void Dispose()
    {
        if (ownerReleased) return;
        ownerReleased = true;
        Release();
    }
}
