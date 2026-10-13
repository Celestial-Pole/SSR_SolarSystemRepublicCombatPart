#version 450
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec3 vWorldPos;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    mat4 _SSRMapViewProjection;
    float _SurfaceDepthRange;
};
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _SurfaceHeightTex;
//保留预乘透明和建筑层次，独立弹体按表面高度写入原有层间深度。
void main()
{
    vec4 color = texture(_MainTex, vUv);
    if (color.a < 0.001) discard;
    float height = texture(_SurfaceHeightTex, vUv).r;
    vec3 world = vWorldPos;
    world.y += _SurfaceDepthRange * height / (1 + abs(height));
    vec4 projected = _SSRMapViewProjection * vec4(world, 1);
    gl_FragDepth = projected.z / projected.w;
    outColor = color;
}
