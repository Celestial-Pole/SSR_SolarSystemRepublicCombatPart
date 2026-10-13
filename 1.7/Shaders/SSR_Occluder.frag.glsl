#version 450
layout(location = 2) in vec3 vWorldPos;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms { float _ClipGround; };
//此通道只写实体深度，颜色写入由材质管线关闭。
void main()
{
    if (_ClipGround > 0.5 && vWorldPos.y < 0) discard;
    outColor = vec4(0);
}
