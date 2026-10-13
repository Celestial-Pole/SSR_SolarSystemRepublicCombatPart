using System;
using UnityEngine;

namespace SSR.UnityComponent.Outline
{
    //管理颜色、几何、阴影滤波和描边合成缓冲，统一释放显存。
    internal sealed class OutlineBuffers : IDisposable
    {
        internal RenderTexture Color;
        internal RenderTexture Geometry;
        internal RenderTexture Composite;
        internal RenderTexture Contact;
        internal RenderTexture ContactScratch;

        //根据输出分辨率和超采样倍率创建中间缓冲。
        internal void Ensure(int width, int height)
        {
            if (Color != null && Color.width == width && Color.height == height) return;
            Dispose();
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
                throw new NotSupportedException("当前显卡不支持描边所需的 ARGBFloat 几何缓冲。");
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RHalf))
                throw new NotSupportedException("当前显卡不支持接触阴影所需的 RHalf 缓冲。");
            Color = Create("炮塔颜色", width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Geometry = Create("炮塔表面位置与编码法线", width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            Geometry.filterMode = FilterMode.Point;
            Composite = Create("炮塔描边合成", width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Contact = Create("炮塔接触阴影", width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            ContactScratch = Create("炮塔阴影滤波", width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
        }

        //创建禁止重复寻址的临时渲染纹理。
        private static RenderTexture Create(string label, int width, int height, int depth,
            RenderTextureFormat format, RenderTextureReadWrite readWrite)
        {
            var texture = new RenderTexture(width, height, depth, format, readWrite)
            {
                name = label,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false
            };
            if (!texture.Create()) throw new InvalidOperationException("无法创建渲染纹理：" + label);
            return texture;
        }

        //销毁中间缓冲，保留由场景引用的最终输出纹理。
        public void Dispose()
        {
            Release(Color);
            Release(Geometry);
            Release(Composite);
            Release(Contact);
            Release(ContactScratch);
            Color = Geometry = Composite = Contact = ContactScratch = null;
        }

        //在编辑器和运行时采用对应的对象销毁方式。
        internal static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is RenderTexture texture) texture.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
