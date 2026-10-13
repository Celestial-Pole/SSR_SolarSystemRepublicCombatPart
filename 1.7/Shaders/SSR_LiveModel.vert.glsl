#version 450
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 1) in vec3 inNormal; // @vertex Normal optional @default = vec4(0, 1, 0, 0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(set = 0, binding = 0) uniform VertexUniforms { mat4 ViewProj; } uVertex;
#include "SSR_LiveModelUniforms.glsl"
layout(location = 2) out vec3 vWorldPos;
layout(location = 3) out vec3 vNormal;
layout(location = 5) out vec3 vCapturePos;
//直接投影网格，固定俯视坐标与原地图面片的坐标一一对应。
void main()
{
    vec4 world = push.modelMatrix * vec4(inPosition, 1);
    vec3 capture = (_SSRView * world).xyz;
    vec3 ground = _SSRMapCenter.xyz + vec3(capture.x, 0, capture.y);
    gl_Position = uVertex.ViewProj * vec4(ground, 1);
    //共享附件只比较实体内部真实深度及整座实体的地图排序。
    float surfaceDepth = clamp((capture.z + _SSRDepth.w) / (_SSRDepth.w - _SSRDepth.z), 0, 1);
    gl_Position.z = (_SSRDepth.x + surfaceDepth * _SSRDepth.y) * gl_Position.w;
    vWorldPos = world.xyz;
    vNormal = normalize(transpose(inverse(mat3(push.modelMatrix))) * inNormal);
    vCapturePos = capture;
}
