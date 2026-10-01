using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //记录参考图对应的正交俯视方向和外描边比例，玩家缩放只影响采样精度。
    internal static class TurretCaptureProfile
    {
        private const int ReferenceResolution = 512;
        internal const float ViewElevation = 60f;
        internal const float FramingMargin = 1.12f;
        internal static readonly Quaternion Direction = Quaternion.Euler(ViewElevation, 0, 0);

        //近景直接保留原生采集精度，中远景用两倍采样处理亚像素轮廓。
        internal static int SupersamplingFor(int resolution)
        {
            return resolution < 2048 ? 2 : 1;
        }

        //将预览线宽换算到实际采集精度，分辨率档位不改变描边占模型的比例。
        internal static void Apply(Material material, float diameter, int resolution)
        {
            int supersampling = SupersamplingFor(resolution);
            float lineScale = (float)resolution / ReferenceResolution * supersampling;
            material.SetFloat("_SilhouetteWidth", 3.7f * lineScale);
            material.SetFloat("_StructureWidth", 0);
            material.SetFloat("_NormalThreshold", 1f - Mathf.Cos(45 * Mathf.Deg2Rad));
            material.SetFloat("_DepthThreshold", Mathf.Max(0.000001f, diameter * 0.002f));
            material.SetColor("_OutlineColor", Color.black);
            material.SetFloat("_Supersampling", supersampling);
            material.SetFloat("_OutputPremultiplied", 1);
        }
    }
}
