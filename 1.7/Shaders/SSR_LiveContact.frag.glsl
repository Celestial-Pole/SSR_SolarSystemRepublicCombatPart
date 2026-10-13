#version 450
#define SSR_LIVE
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "SSR_CompositeCommon.glsl"
//同一实体保持原来的三十二点接触遮蔽，其他实体的覆盖区域交给它自己的步骤。
void main()
{
    if (Geometry(vUv).a == 0) discard;
    outColor = CaptureTurretContact(v2f_img(vUv));
}
