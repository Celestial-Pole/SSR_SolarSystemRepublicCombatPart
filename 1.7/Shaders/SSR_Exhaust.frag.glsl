#version 450
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec3 vWorldPos;
layout(location = 4) in vec2 vData;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @default = vec4(0.6, 0.6, 0.6, 0.6) @color
    float _Flame;
    float _Age;
    float _Seed;
    float _Clock;
    float _VertexData;
    float _Trail;
    float _Opacity; // @default = 1
    float _FlameBrightness; // @default = 1.35
};
struct Varyings { vec2 uv; float height; vec2 data; };
float saturate(float x) { return clamp(x, 0.0, 1.0); }
            float Hash(vec2 p)
            {
                return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453);
            }
            //平滑插值相邻格点，避免烟团出现清晰的方格边界。
            float Noise(vec2 p)
            {
                vec2 cell = floor(p), t = fract(p);
                t = t * t * (3 - 2 * t);
                return mix(mix(Hash(cell), Hash(cell + vec2(1,0)), t.x),
                    mix(Hash(cell + vec2(0,1)), Hash(cell + 1), t.x), t.y);
            }
            //输出预乘烟焰颜色，不参与实体法线、接触阴影或描边。
            vec4 Frag(Varyings sampleData) 
            {
                if (sampleData.height < 0) discard;
                vec2 p = sampleData.uv * 2 - 1;
                float age = sampleData.data.x, seed = sampleData.data.y;
                float alpha;
                vec3 color;
                if (_Flame > 0.5)
                {
                    //喷流中心和边缘沿气流方向摆动，直飞时也不形成均匀硬直的三角形。
                    float flow = Noise(vec2(sampleData.uv.y * 8 - _Clock * 15, seed * 0.19));
                    p.x += sin(sampleData.uv.y * 13 - _Clock * 46 + seed) * (1 - sampleData.uv.y) * 0.12;
                    float width = mix(0.12, 0.7, sampleData.uv.y) * (0.7 + flow * 0.4);
                    alpha = pow(saturate(1 - abs(p.x) / width), 0.85) * smoothstep(0, 0.22, sampleData.uv.y);
                    if (_Trail > 0.5)
                        alpha *= 0.72 * (1 - smoothstep(0.45, 1, age));
                    else
                        alpha *= 1 - smoothstep(0.97, 1, sampleData.uv.y);
                    alpha *= 0.82 + flow * 0.18;
                    //扩大偏白的高温焰芯，再按配置提亮，外围仍保留橙色和透明渐变。
                    color = mix(vec3(1,0.22,0.035), vec3(1,0.985,0.82), pow(sampleData.uv.y, 1.3)) * _FlameBrightness;
                }
                else
                {
                    //连续多尺度云纹同时控制轮廓和明暗，烟团扩散后仍保留团块层次。
                    vec2 offset = vec2(seed * 0.17, seed * 0.31) + age * vec2(0.4,-0.25);
                    float cloud = Noise(p * 2.4 + offset) * 0.7 + Noise(p * 5.3 + offset) * 0.3;
                    float density = saturate(1 - dot(p,p) + (cloud - 0.5) * 0.55);
                    alpha = smoothstep(0, 0.65, density) * (0.62 + cloud * 0.38)
                        * pow(saturate(1 - age), 0.7) * _Color.a;
                    color = _Color.rgb * (0.76 + cloud * 0.3);
                }
                alpha *= _Opacity;
                return vec4(color * alpha, alpha);
            }

//按原来顶点参数的插值规则接入手工烟焰网格。
void main() { outColor = Frag(Varyings(vUv, vWorldPos.y, mix(vec2(_Age, _Seed), vData, _VertexData))); }
