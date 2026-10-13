#version 450
layout(location = 2) in vec3 vWorldPos;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1, 1, 1, 1)
    float _ClipGround;
};
//模型原色与描边光照分开处理，保持原来的不透明底色采集。
void main()
{
    if (_ClipGround > 0.5 && vWorldPos.y < 0) discard;
    outColor = vec4(_Color.rgb, 1);
}
