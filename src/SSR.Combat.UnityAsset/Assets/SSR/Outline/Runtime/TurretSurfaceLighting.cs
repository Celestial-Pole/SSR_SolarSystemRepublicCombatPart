using UnityEngine;

namespace SSR.UnityComponent.Outline
{
    //为游戏和美术预览提供一致的哑光照明参数，使用已有几何缓冲塑造表面体积。
    internal static class TurretSurfaceLighting
    {
        private static readonly Vector3 KeyDirection = new Vector3(-0.6f, 1f, -0.45f).normalized;
        private static readonly Vector3 FillDirection = new Vector3(0.8f, 0.5f, 0.2f).normalized;
        private const float ContactRadiusRatio = 0.035f;
        private const float ContactStrength = 0.5f;

        //将固定世界光向转换到几何缓冲所在的观察空间，遮蔽半径仅随模型尺寸缩放。
        internal static void Apply(Material material, Camera camera, float diameter, bool enabled = true)
        {
            var view = camera.worldToCameraMatrix;
            var projection = camera.projectionMatrix;
            material.SetFloat("_SurfaceLighting", enabled ? 1 : 0);
            material.SetVector("_SurfaceKeyDirection", view.MultiplyVector(KeyDirection).normalized);
            material.SetVector("_SurfaceFillDirection", view.MultiplyVector(FillDirection).normalized);
            material.SetVector("_SurfaceUpDirection", view.MultiplyVector(Vector3.up).normalized);
            //投影矩阵给出单位世界长度对应的纹理跨度，避免将阴影半径绑定到像素档位。
            material.SetVector("_SurfaceProjection", new Vector4(Mathf.Abs(projection.m00) * 0.5f,
                Mathf.Abs(projection.m11) * 0.5f, camera.orthographic ? 1 : 0, 0));
            material.SetFloat("_SurfaceContactRadius", diameter * ContactRadiusRatio);
            material.SetFloat("_SurfaceContactStrength", ContactStrength);
        }

        //先生成接触阴影，再沿两个方向做保边滤波，颜色与外轮廓始终使用各自原始缓冲。
        internal static void RenderContact(OutlineBuffers buffers, Material material)
        {
            OutlineColorBlit.Draw(buffers.Geometry, buffers.Contact, material, 2);
            material.SetVector("_ContactBlurDirection", new Vector4(1, 0, 0, 0));
            OutlineColorBlit.Draw(buffers.Contact, buffers.ContactScratch, material, 3);
            material.SetVector("_ContactBlurDirection", new Vector4(0, 1, 0, 0));
            OutlineColorBlit.Draw(buffers.ContactScratch, buffers.Contact, material, 3);
            material.SetTexture("_ContactVisibilityTex", buffers.Contact);
        }
    }
}
