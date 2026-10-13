#version 450
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
//烟焰保留预乘透明，实体遮挡已由同帧的深度通道完成。
void main()
{
    vec4 color = texture(_MainTex, vUv);
    if (color.a < 0.001) discard;
    outColor = color;
}
