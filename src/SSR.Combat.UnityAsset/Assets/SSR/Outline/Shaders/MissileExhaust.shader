Shader "SSR/Missiles/Exhaust"
{
    Properties
    {
        _Flame ("尾焰模式", Float) = 0
        _Color ("颜色", Color) = (0.6,0.6,0.6,0.6)
        _VertexData ("逐顶点年龄", Float) = 0
        _Trail ("历史热尾流", Float) = 0
        _Opacity ("浓度", Float) = 1
        _FlameBrightness ("尾焰亮度", Float) = 1.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float _Flame, _Age, _Seed, _Clock, _VertexData, _Trail, _Opacity, _FlameBrightness;
            float4 _Color;
            //传递烟焰局部坐标和实际高度，地下部分始终透明。
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                float2 data : TEXCOORD2;
            };
            //投影手动生成的烟团和尾焰面片。
            Varyings Vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.height = mul(unity_ObjectToWorld, input.vertex).y;
                output.data = lerp(float2(_Age, _Seed), input.texcoord1.xy, _VertexData);
                return output;
            }
            //为连续噪声提供固定格点值，形态随机不依赖系统时钟。
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }
            //平滑插值相邻格点，避免烟团出现清晰的方格边界。
            float Noise(float2 p)
            {
                float2 cell = floor(p), t = frac(p);
                t = t * t * (3 - 2 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), t.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), t.x), t.y);
            }
            //输出预乘烟焰颜色，不参与实体法线、接触阴影或描边。
            float4 Frag(Varyings input) : SV_Target
            {
                clip(input.height);
                float2 p = input.uv * 2 - 1;
                float age = input.data.x, seed = input.data.y;
                float alpha;
                float3 color;
                if (_Flame > 0.5)
                {
                    //喷流中心和边缘沿气流方向摆动，直飞时也不形成均匀硬直的三角形。
                    float flow = Noise(float2(input.uv.y * 8 - _Clock * 15, seed * 0.19));
                    p.x += sin(input.uv.y * 13 - _Clock * 46 + seed) * (1 - input.uv.y) * 0.12;
                    float width = lerp(0.12, 0.7, input.uv.y) * (0.7 + flow * 0.4);
                    alpha = pow(saturate(1 - abs(p.x) / width), 0.85) * smoothstep(0, 0.22, input.uv.y);
                    if (_Trail > 0.5)
                        alpha *= 0.72 * (1 - smoothstep(0.45, 1, age));
                    else
                        alpha *= 1 - smoothstep(0.97, 1, input.uv.y);
                    alpha *= 0.82 + flow * 0.18;
                    //扩大偏白的高温焰芯，再按配置提亮，外围仍保留橙色和透明渐变。
                    color = lerp(float3(1,0.22,0.035), float3(1,0.985,0.82), pow(input.uv.y, 1.3)) * _FlameBrightness;
                }
                else
                {
                    //连续多尺度云纹同时控制轮廓和明暗，烟团扩散后仍保留团块层次。
                    float2 offset = float2(seed * 0.17, seed * 0.31) + age * float2(0.4,-0.25);
                    float cloud = Noise(p * 2.4 + offset) * 0.7 + Noise(p * 5.3 + offset) * 0.3;
                    float density = saturate(1 - dot(p,p) + (cloud - 0.5) * 0.55);
                    alpha = smoothstep(0, 0.65, density) * (0.62 + cloud * 0.38)
                        * pow(saturate(1 - age), 0.7) * _Color.a;
                    color = _Color.rgb * (0.76 + cloud * 0.3);
                }
                alpha *= _Opacity;
                return float4(color * alpha, alpha);
            }
            ENDCG
        }
    }
}
