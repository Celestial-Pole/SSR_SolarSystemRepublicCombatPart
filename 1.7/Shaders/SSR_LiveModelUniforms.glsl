layout(set = 2, binding = 0) uniform MaterialUniforms
{
    mat4 _SSRView;
    vec4 _SSRMapCenter;
    vec4 _SSRDepth;
    vec4 _Color;
    float _ClipGround;
    float _RotatingSurface;
    float _SSRObjectId;
};
