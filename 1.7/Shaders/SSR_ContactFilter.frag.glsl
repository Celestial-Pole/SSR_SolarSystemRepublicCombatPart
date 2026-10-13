#version 450
layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "SSR_CompositeCommon.glsl"
//使用原始公式执行单个合成通道。
void main() { outColor = FilterTurretContact(v2f_img(vUv)); }
