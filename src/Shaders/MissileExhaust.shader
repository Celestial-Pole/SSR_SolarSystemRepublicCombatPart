Shader "SSR/Missiles/Exhaust"
{
    Properties
    {
        _Flame ("尾焰模式", Float) = 0
        _Color ("烟雾颜色", Color) = (0.76,0.74,0.68,1)
        _VertexData ("逐顶点年龄", Float) = 0
        _Trail ("历史热尾流", Float) = 0
        _Opacity ("浓度", Float) = 1
        _FlameBrightness ("尾焰亮度", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
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
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                float2 data : TEXCOORD2;
            };

            //烟团顶点携带独立年龄与种子，流动只使用游戏时间。
            Varyings Vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.height = mul(unity_ObjectToWorld, input.vertex).y;
                output.data = lerp(float2(_Age, _Seed), input.texcoord1.xy, _VertexData);
                return output;
            }

            //固定格点随机值，避免暂停后噪声仍在播放。
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453);
            }

            //连续噪声提供滚动云团与柔和的火焰边缘。
            float Noise(float2 p)
            {
                float2 cell = floor(p), t = frac(p);
                t = t * t * (3 - 2 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), t.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), t.x), t.y);
            }

            //三层噪声同时保留大块翻卷和细碎边缘。
            float Cloud(float2 p)
            {
                return Noise(p) * 0.57 + Noise(p * 2.07 + 17.3) * 0.29
                    + Noise(p * 4.19 - 8.1) * 0.14;
            }

            //从窄喷口扩张为白热焰芯，尾端破碎收尖，外围保留低透明度暖色光晕。
            float4 Flame(float2 uv, float age, float seed)
            {
                float t = 1 - uv.y;
                float2 flow = float2((uv.x - 0.5) * 4, t * 9 - _Clock * 12);
                float noise = Cloud(flow + seed * 0.13);
                float bend = (Noise(float2(t * 5 - _Clock * 7, seed)) - 0.5) * 0.16 * t;
                float x = abs(uv.x * 2 - 1 + bend);
                float envelope = (0.22 + 0.48 * sin(pow(t, 0.7) * 3.14159))
                    * (0.84 + 0.26 * noise);
                float radial = x / max(envelope, 0.04);
                float endFade = 1 - smoothstep(0.74, 1, t + (noise - 0.5) * 0.12);
                float body = 1 - smoothstep(0.35, 1, radial);
                float core = (1 - smoothstep(0.1, 0.56, radial)) * (1 - smoothstep(0.7, 1, t));
                float halo = exp(-radial * radial * 2.3) * 0.14;
                float alpha = saturate(body * (0.82 + noise * 0.18) + halo) * endFade;
                float3 color = lerp(float3(1,0.31,0.035), float3(1,0.78,0.26), body);
                color = lerp(color, float3(1,0.985,0.88), core);
                if (_Trail > 0.5) alpha *= 1 - smoothstep(0.4, 1, age);
                alpha *= _Opacity;
                return float4(color * _FlameBrightness * alpha, alpha);
            }

            //不规则云团边界配合内部明暗，年轻烟气略暖，离开喷口后逐渐冷却。
            float4 Smoke(float2 uv, float age, float seed)
            {
                float2 p = uv * 2 - 1;
                float angle = seed * 2.39996;
                p = float2(p.x * cos(angle) - p.y * sin(angle), p.x * sin(angle) + p.y * cos(angle));
                float2 flow = p * 2.6 + float2(seed * 0.17, seed * 0.31) + age * float2(0.35,-0.28);
                float cloud = Cloud(flow);
                float edge = 0.77 + (Noise(flow * 0.85) - 0.5) * 0.3;
                float radius = length(p);
                float silhouette = 1 - smoothstep(edge - 0.27, edge + 0.13, radius);
                float shade = saturate(0.53 + cloud * 0.48 - p.y * 0.1);
                float fade = smoothstep(0, 0.045, age) * (1 - smoothstep(0.42, 1, age));
                float alpha = silhouette * (0.56 + cloud * 0.44) * fade * _Color.a * _Opacity;
                float3 color = lerp(_Color.rgb * 0.53, _Color.rgb * 1.17, shade);
                color = lerp(color, float3(0.95,0.68,0.32), exp(-age * 22) * 0.42);
                return float4(color * alpha, alpha);
            }

            //预乘透明采集只参与实体深度遮挡，不进入炮塔描边或接触阴影。
            float4 Frag(Varyings input) : SV_Target
            {
                clip(input.height);
                if (_Flame > 0.5) return Flame(input.uv, input.data.x, input.data.y);
                return Smoke(input.uv, input.data.x, input.data.y);
            }
            ENDCG
        }
    }
}
