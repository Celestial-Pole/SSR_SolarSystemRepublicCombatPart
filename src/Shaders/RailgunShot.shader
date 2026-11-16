Shader "SSR/Railguns/Shot"
{
    Properties
    {
        _Dissolve ("整体消散", Range(0,1)) = 0
        _MuzzleAge ("炮口时间", Float) = 0
        _Seed ("形态种子", Float) = 0
        _Clock ("烟雾时间", Float) = 0
        _SmokeScale ("烟团尺度", Float) = 1
        _RingLife ("烟环寿命", Float) = 1.5
        _FlashLife ("蓝光寿命", Float) = .32
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
            float _Dissolve, _MuzzleAge, _Seed, _Clock, _SmokeScale, _RingLife, _FlashLife;
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float kind : TEXCOORD1; float phase : TEXCOORD2; };

            //几何已经按真实炮口俯仰投影，材质只负责柔边与消散。
            Varyings Vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.kind = input.texcoord1.x;
                output.phase = input.texcoord1.y;
                return output;
            }

            //相邻采样连续的噪声，让整条尾迹自然破散。
            float Noise(float2 p)
            {
                float2 c = floor(p), t = frac(p);
                t = t*t*(3-2*t);
                float4 n = frac(sin(float4(dot(c,float2(127.1,311.7)), dot(c+float2(1,0),float2(127.1,311.7)),
                    dot(c+float2(0,1),float2(127.1,311.7)),dot(c+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(n.x,n.y,t.x),lerp(n.z,n.w,t.x),t.y);
            }

            //叠加大块翻卷与细小絮状纹理，噪声随游戏时间缓慢流动。
            float Cloud(float2 p)
            {
                return Noise(p)*.57 + Noise(p*2.03+13.7)*.29 + Noise(p*4.17-7.3)*.14;
            }

            //每团烟具有球形明暗和絮状边缘，相互遮叠后组成厚实的三维烟环。
            float4 SmokePuff(float2 uv, float phase)
            {
                float age = saturate(_MuzzleAge/_RingLife);
                float2 p = uv*2-1;
                float sine = sin(phase), cosine = cos(phase);
                float2 q = float2(p.x*cosine-p.y*sine,p.x*sine+p.y*cosine);
                float2 flow = float2(_MuzzleAge*.6,-_MuzzleAge*.45);
                float cloud = Cloud(q*2.8+phase+flow);
                float detail = Cloud(q*6.4-phase-flow);
                float radius = length(p)+(.5-cloud)*.22;
                float sphere = sqrt(saturate(1-radius*radius));
                float edge = 1-smoothstep(.68,1,radius);
                float breakup = lerp(1,smoothstep(.2,.65,cloud),smoothstep(.35,1,age));
                float density = sphere*(.5+detail*.9)*breakup;
                float fade = 1-smoothstep(.12,1,age);
                float alpha = (1-exp(-density*2.8))*edge*fade*.73;
                float3 normal = normalize(float3(p,max(.08,sphere)));
                float shade = saturate(.16+dot(normal,float3(-.35,.65,.675))*.65+detail*.25);
                float3 color = lerp(float3(.46,.50,.56),float3(.99,.995,1),shade);
                color += float3(.02,.10,.22)*exp(-_MuzzleAge*14)*sphere;
                return float4(color,saturate(alpha));
            }

            //白热亮芯与饱和蓝光直接叠加，夜间仍明亮，光晕不会遮暗炮口。
            float4 MuzzleFlash(float2 uv, float kind)
            {
                float2 p = uv*2-1;
                float pulse = 1-smoothstep(.045,_FlashLife,_MuzzleAge);
                float edge = 1-smoothstep(.82,1,length(p));
                float3 emission;
                if (kind > 2.5)
                {
                    float width = lerp(.52,.12,saturate((p.y+.35)/1.35));
                    float beam = exp(-p.x*p.x/(width*width)*3.5);
                    beam *= smoothstep(-.65,-.3,p.y)*(1-smoothstep(.15,1,p.y));
                    float core = exp(-p.x*p.x*100-(p.y+.15)*(p.y+.15)*24);
                    emission = float3(.08,.85,2.6)*beam + float3(2.4,2.7,3.1)*core;
                }
                else
                {
                    float r2 = dot(p,p);
                    float halo = exp(-r2*4.6);
                    float core = exp(-r2*42);
                    float rays = exp(-p.x*p.x*140-p.y*p.y*7)
                        + exp(-p.y*p.y*140-p.x*p.x*7);
                    emission = float3(.04,.42,1.8)*halo + float3(.1,.55,1.2)*rays*.55
                        + float3(2.4,2.7,3.2)*core;
                }
                //预乘混合下零透明度只累加发光，不依赖场景光照。
                return float4(emission*pulse*edge,0);
            }

            //整条尾迹共用消散进度，横向云团持续翻卷，随后整体扩散破散。
            float4 SmokeTrail(float2 uv)
            {
                float x = uv.x*2-1;
                float along = uv.y/_SmokeScale;
                float2 p = float2(along,x*1.8);
                float2 flow = float2(-_Clock*.26,_Clock*.19);
                float cloud = Cloud(p*.72+_Seed+flow);
                float detail = Cloud(p*float2(1.1,1.5)-_Seed-flow*.7);
                float roll = (Noise(float2(along*.58+_Seed,_Clock*.48))-.5)*.3;
                float envelope = lerp(.48,.9,cloud);
                float radial = (x+roll)/envelope;
                float density = exp(-radial*radial*2)*(1-smoothstep(.72,1,abs(x)));
                density *= .28+.72*smoothstep(.13,.77,detail);
                float breakup = smoothstep(_Dissolve*.95-.14,_Dissolve*.95+.15,cloud*.55+detail*.45);
                float alpha = density*lerp(1,breakup,smoothstep(.15,.6,_Dissolve))*pow(1-_Dissolve,1.1);
                float shade = saturate(.32+detail*.68-x*.1);
                float3 color = lerp(float3(.74,.77,.8),float3(.99,.995,1),shade);
                return float4(color,saturate(alpha));
            }

            //烟雾使用预乘透明，蓝光通过同一混合状态叠加到炮口。
            float4 Frag(Varyings input) : SV_Target
            {
                float alpha;
                float3 color = 1;
                if (input.kind > 1.5)
                {
                    return MuzzleFlash(input.uv,input.kind);
                }
                else if (input.kind > .5)
                {
                    float4 smoke = SmokePuff(input.uv,input.phase);
                    color = smoke.rgb;
                    alpha = smoke.a;
                }
                else
                {
                    float4 smoke = SmokeTrail(input.uv);
                    color = smoke.rgb;
                    alpha = smoke.a;
                }
                alpha = saturate(alpha);
                return float4(color*alpha,alpha);
            }
            ENDCG
        }
    }
}
