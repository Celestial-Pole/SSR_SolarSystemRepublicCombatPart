using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //统一正交俯视方向和地图描边宽度。
    internal static class TurretCaptureProfile
    {
        internal const float ViewElevation = 60f;
        internal const float FramingMargin = 1.12f;
        internal const float OutlineWidth = 0.05f;
        internal static readonly Quaternion Direction = Quaternion.Euler(ViewElevation, 0, 0);

        //近景直接保留原生采集精度，中远景用两倍采样处理亚像素轮廓。
        internal static int SupersamplingFor(int resolution)
        {
            return resolution < 2048 ? 2 : 1;
        }

        //将固定地图线宽换算为采集像素，模型大小和取景范围不改变黑边粗细。
        internal static void Apply(Material material, float diameter, int resolution, float mapSize)
        {
            int supersampling = SupersamplingFor(resolution);
            material.SetFloat("_SilhouetteWidth", OutlineWidth * resolution * supersampling / mapSize);
            material.SetFloat("_StructureWidth", 0);
            material.SetFloat("_NormalThreshold", 1f - Mathf.Cos(45 * Mathf.Deg2Rad));
            material.SetFloat("_DepthThreshold", Mathf.Max(0.000001f, diameter * 0.002f));
            material.SetColor("_OutlineColor", Color.black);
            material.SetFloat("_Supersampling", supersampling);
            material.SetFloat("_OutputPremultiplied", 1);
        }
    }
}
