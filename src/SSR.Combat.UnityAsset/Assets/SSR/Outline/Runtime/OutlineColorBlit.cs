using UnityEngine;

namespace SSR.UnityComponent.Outline
{
    //管理颜色合成的目标色彩空间，防止嵌套相机继承其他绘制阶段的颜色写入状态。
    internal static class OutlineColorBlit
    {
        //依据目标纹理的格式执行颜色转换，并恢复调用者的全局渲染状态。
        internal static void Draw(RenderTexture source, RenderTexture destination, Material material, int pass)
        {
            bool previous = GL.sRGBWrite;
            GL.sRGBWrite = destination.sRGB;
            try { Graphics.Blit(source, destination, material, pass); }
            finally { GL.sRGBWrite = previous; }
        }
    }
}
