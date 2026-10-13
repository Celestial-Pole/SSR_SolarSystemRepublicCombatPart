#version 450
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 required
layout(location = 0) out vec4 vColor;
layout(location = 1) out vec2 vUv;
//与离屏相机一致地保持纹理下边为 UV 零，避免每次合成翻转影像。
void main()
{
    gl_Position = vec4(inPosition.x * 2, inPosition.z * 2, 0, 1);
    vColor = vec4(1);
    vUv = inUv0.xy;
}
