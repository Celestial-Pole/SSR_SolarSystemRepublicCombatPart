#version 450
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 1) in vec3 inNormal; // @vertex Normal optional @default = vec4(0, 1, 0, 0)
layout(location = 2) in vec4 inColor; // @vertex Color optional @default = vec4(1, 1, 1, 1)
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0, 0, 0, 0)
layout(location = 4) in vec4 inUv1; // @vertex TexCoord1 optional @default = vec4(0, 0, 0, 0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set = 0, binding = 0) uniform VertexUniforms { mat4 ViewProj; } uVertex;
layout(location = 0) out vec4 vColor;
layout(location = 1) out vec2 vUv;
layout(location = 2) out vec3 vWorldPos;
layout(location = 3) out vec3 vNormal;
layout(location = 4) out vec2 vData;
//沿用原模型矩阵；非等比缩放时按逆转置变换法线。
void main()
{
    vec4 world = push.modelMatrix * vec4(inPosition, 1);
    gl_Position = uVertex.ViewProj * world;
    vWorldPos = world.xyz;
    vNormal = normalize(transpose(inverse(mat3(push.modelMatrix))) * inNormal);
    vColor = inColor;
    vUv = inUv0.xy;
    vData = inUv1.xy;
}
