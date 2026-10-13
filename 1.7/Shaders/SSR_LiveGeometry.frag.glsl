#version 450
layout(location = 2) in vec3 vWorldPos;
layout(location = 3) in vec3 vNormal;
layout(location = 5) in vec3 vCapturePos;
layout(location = 0) out vec4 outColor;
#include "SSR_LiveModelUniforms.glsl"
//法线精度及转轮标记沿用原来的二十四位八面体编码。
float PackNormal(vec3 normal)
{
    normal /= abs(normal.x) + abs(normal.y) + abs(normal.z);
    vec2 oct = normal.xy;
    if (normal.z < 0)
        oct = (1 - abs(oct.yx)) * vec2(oct.x >= 0 ? 1 : -1, oct.y >= 0 ? 1 : -1);
    vec2 encoded = floor(clamp(oct * 0.5 + 0.5, vec2(0), vec2(1)) * 4095 + 0.5);
    return encoded.x + encoded.y * 4096 + 1;
}
//地图投影保留 Vulkan 的 Y 方向，正反面与原离屏翻转投影相反。
void main()
{
    if (_ClipGround > 0.5 && vWorldPos.y < 0) discard;
    vec3 normal = normalize(mat3(_SSRView) * vNormal) * (gl_FrontFacing ? 1 : -1);
    float encoded = PackNormal(normal);
    outColor = vec4(vCapturePos, _RotatingSurface > 0.5 ? -encoded : encoded);
}
