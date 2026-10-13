#version 450
#define SSR_LIVE
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "SSR_CompositeCommon.glsl"
//沿取景坐标的两个轴执行原来的双边滤波，镜头旋转不会改变滤波方向。
void main()
{
    if (Geometry(vUv).a == 0) discard;
    outColor = FilterTurretContact(v2f_img(vUv));
}
