#version 450
layout(location = 2) in vec3 vWorldPos;
layout(location = 0) out vec4 outColor;
#include "SSR_LiveModelUniforms.glsl"
//原色和对象编号共用浮点附件，采样时不会把其他炮塔当作本体。
void main()
{
    if (_ClipGround > 0.5 && vWorldPos.y < 0) discard;
    outColor = vec4(_Color.rgb, _SSRObjectId);
}
