using UnityEngine;

namespace SSR.Combat.Offscreen
{
    //按完整面片的屏幕占用选择纹理精度，透明留白与模型实体使用同一像素密度。
    internal static class TurretScreenResolution
    {
        private const int Minimum = 256;
        private const int Maximum = 4096;
        private const float PixelsPerScreenPixel = 1.5f;

        //投影面片四边并预留采样余量，降档采用滞回阈值以避免缩放边界反复分配。
        internal static int Select(Camera camera, Vector3 center, float size, int current)
        {
            float half = size * 0.5f;
            Vector2 bottomLeft = camera.WorldToScreenPoint(center + new Vector3(-half, 0, -half));
            Vector2 bottomRight = camera.WorldToScreenPoint(center + new Vector3(half, 0, -half));
            Vector2 topRight = camera.WorldToScreenPoint(center + new Vector3(half, 0, half));
            Vector2 topLeft = camera.WorldToScreenPoint(center + new Vector3(-half, 0, half));
            float pixels = Mathf.Max(Vector2.Distance(bottomLeft, bottomRight), Vector2.Distance(topLeft, topRight));
            pixels = Mathf.Max(pixels, Mathf.Max(Vector2.Distance(bottomLeft, topLeft), Vector2.Distance(bottomRight, topRight)));
            float required = pixels * PixelsPerScreenPixel;
            int limit = Mathf.Min(Maximum, SystemInfo.maxTextureSize);
            int desired = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(required)), Minimum, limit);
            if (current > desired && required > current * 0.375f) return current;
            return desired;
        }
    }
}
