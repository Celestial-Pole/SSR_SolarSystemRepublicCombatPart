#version 450
#define SSR_LIVE
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec3 vWorldPos;
layout(location = 0) out vec4 outColor;
#include "SSR_CompositeCommon.glsl"
//描边半径保持 0.05 格，在最终屏幕像素内合并四个覆盖率样本。
void main()
{
    vec2 dx = (_SSRCaptureFromScreen * vec4(_SSRScreenTexelSize.x * 0.5, 0, 0, 0)).xy;
    vec2 dy = (_SSRCaptureFromScreen * vec4(0, _SSRScreenTexelSize.y * 0.5, 0, 0)).xy;
    vec4 color = (Composite(v2f_img(vUv + dx + dy)) + Composite(v2f_img(vUv + dx - dy))
        + Composite(v2f_img(vUv - dx + dy)) + Composite(v2f_img(vUv - dx - dy))) * 0.25;
    if (color.a < 0.001) discard;
    float height = 0;
    if (_SurfaceDepthRange != 0)
        height = max(max(SurfaceHeightAt(vUv + dx + dy), SurfaceHeightAt(vUv + dx - dy)),
            max(SurfaceHeightAt(vUv - dx + dy), SurfaceHeightAt(vUv - dx - dy)));
    vec3 world = vWorldPos;
    world.y += _SurfaceDepthRange * height / (1 + abs(height));
    vec4 projected = _SSRMapViewProjection * vec4(world, 1);
    gl_FragDepth = projected.z / projected.w;
    outColor = color;
}
