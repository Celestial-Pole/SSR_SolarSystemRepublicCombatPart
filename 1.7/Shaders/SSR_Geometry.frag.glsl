#version 450
layout(location = 2) in vec3 vWorldPos;
layout(location = 3) in vec3 vNormal;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    mat4 _SSRView;
    float _ClipGround;
    float _RotatingSurface;
};
//以原来的二十四位八面体编码保存法线，负号标记高速转轮。
float PackNormal(vec3 normal)
{
    normal /= abs(normal.x) + abs(normal.y) + abs(normal.z);
    vec2 oct = normal.xy;
    if (normal.z < 0)
        oct = (1 - abs(oct.yx)) * vec2(oct.x >= 0 ? 1 : -1, oct.y >= 0 ? 1 : -1);
    vec2 encoded = floor(clamp(oct * 0.5 + 0.5, vec2(0), vec2(1)) * 4095 + 0.5);
    return encoded.x + encoded.y * 4096 + 1;
}
//真实观察空间位置用于接触阴影，不能替换为颜色或归一化深度。
void main()
{
    if (_ClipGround > 0.5 && vWorldPos.y < 0) discard;
    //离屏投影翻转了 Vulkan 的 Y 方向，正反面标记随之反转。
    vec3 normal = normalize(mat3(_SSRView) * vNormal) * (gl_FrontFacing ? -1 : 1);
    float encoded = PackNormal(normal);
    outColor = vec4((_SSRView * vec4(vWorldPos, 1)).xyz, _RotatingSurface > 0.5 ? -encoded : encoded);
}
