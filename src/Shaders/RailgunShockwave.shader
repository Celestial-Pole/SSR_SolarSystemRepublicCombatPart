Shader "SSR/Railguns/Shockwave"
{
    Properties
    {
        _MuzzleAge ("炮口时间", Float) = 0
        _ShockLife ("冲击波寿命", Float) = .55
        _ShockCenter ("冲击波中心", Vector) = (0,0,0,1)
        _Seed ("形态种子", Float) = 0
        _Dissolve ("尾迹消散", Range(0,1)) = 0
        _Clock ("气流时间", Float) = 0
        _SmokeScale ("气流尺度", Float) = 1
        _TrailLength ("尾迹长度", Float) = 1
        _SecondsPerTile ("每格飞行秒数", Float) = 0
        _AirLife ("余热寿命", Float) = 1.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual
        //同一相机每帧共用一次背景抓取，多门炮不重复复制整张屏幕。
        GrabPass { "_SSR_RailgunScene" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _SSR_RailgunScene;
            float4 _ShockCenter;
            float _MuzzleAge, _ShockLife, _Seed;
            float _Dissolve, _Clock, _SmokeScale, _TrailLength;
            float _SecondsPerTile, _AirLife;
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screen : TEXCOORD1;
                float4 center : TEXCOORD2;
                float kind : TEXCOORD3;
            };

            //地图顶点与折射中心经过同一屏幕变换，缩放时波纹不会漂移。
            Varyings Vert(appdata_full input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord.xy;
                output.screen = ComputeGrabScreenPos(output.position);
                output.center = ComputeGrabScreenPos(UnityWorldToClipPos(_ShockCenter.xyz));
                output.kind = input.texcoord1.x;
                return output;
            }

            //空间噪声只塑造裂隙边缘，时间控制一次扩张与冷却。
            float Noise(float2 p)
            {
                float2 cell = floor(p), blend = frac(p);
                blend = blend*blend*(3-2*blend);
                float4 n = frac(sin(float4(dot(cell,float2(127.1,311.7)),dot(cell+float2(1,0),float2(127.1,311.7)),
                    dot(cell+float2(0,1),float2(127.1,311.7)),dot(cell+1,float2(127.1,311.7))))*43758.5453);
                return lerp(lerp(n.x,n.y,blend.x),lerp(n.z,n.w,blend.x),blend.y);
            }

            //炮弹经过后向两侧撑开一次，短暂的余热沿原位消散。
            float4 TrailAir(Varyings input)
            {
                float x = input.uv.x*2-1;
                float along = input.uv.y/_SmokeScale;
                float2 screen = input.screen.xy/input.screen.w;
                //通过屏幕导数计算轨迹横向，任意朝向、缩放和俯仰下都沿正确方向折射。
                float2 gradient = float2(ddx(x),ddy(x));
                float2 normalUV = ddx(screen)*gradient.x+ddy(screen)*gradient.y;
                float2 normalPixels = normalUV*_ScreenParams.xy;
                float2 across = normalPixels*rsqrt(max(dot(normalPixels,normalPixels),1e-12));
                float2 tangent = float2(-across.y,across.x);
                float halfWidth = rcp(max(length(gradient),.0001));
                float age = max(0,_Clock-input.uv.y*_SecondsPerTile);
                float cloud = Noise(float2(x*2.3+_Seed,along*.85));
                float fine = Noise(float2(x*4.8-_Seed,along*2.2));
                float grain = Noise(float2(x*12.7+_Seed*1.7,along*5.8));
                float opening = 1-exp(-age*45);
                //撑开后短暂停留，再单向回落，让高速射击的瞬间也能看清。
                float kick = opening*exp(-max(0,age-.1)*4.2);
                float heat = opening*exp(-age*2.5)*.1;
                float edge = abs(x);
                float front = .18+.3*(1-exp(-age*10));
                //大尺度起伏、中尺度絮状边界和细碎蚀边共同决定轮廓与羽化宽度。
                float boundary = (cloud-.5)*.22+(fine-.5)*.1;
                float halfBand = .105+cloud*.04;
                float feather = .07+fine*.1;
                float contour = abs(edge-front-boundary)-halfBand+(grain-.5)*.055;
                float envelope = 1-smoothstep(-feather,feather,contour);
                float ends = smoothstep(0,_SmokeScale*.8,input.uv.y)
                    *smoothstep(0,_SmokeScale*.8,_TrailLength-input.uv.y);
                float fade = (1-smoothstep(_AirLife*.5,_AirLife,age))*saturate(1-_Dissolve);
                float alpha = envelope*ends*fade*opening*.9;
                clip(alpha-.001);
                //两侧位移始终朝外，脉冲结束后只单向松弛，不反复折返。
                float stretch = sign(x)*(kick*(.8+cloud*.25)+heat*(.5+cloud));
                float shear = (fine-.5)*heat*.35;
                //折射位移与外侧柔边的覆盖宽度分开匹配。
                float strength = clamp(halfWidth*.15,4,12)*fade*ends;
                float2 pixel = rcp(_ScreenParams.xy);
                float2 offset = (-across*stretch+tangent*shear)*strength*pixel;
                float2 border = pixel*.5;
                float2 refracted = clamp(screen+offset,border,1-border);
                //只在噪声羽化区域柔化背景，模糊方向和半径随空间噪声变化。
                float softness = 4*envelope*(1-envelope)*(.65+grain*1.25);
                float2 blur = (across*(.5+grain)+tangent*(fine-.5)*.7)*softness*pixel;
                float3 background = tex2D(_SSR_RailgunScene,refracted).rgb*.5;
                background += tex2D(_SSR_RailgunScene,clamp(refracted+blur,border,1-border)).rgb*.25;
                background += tex2D(_SSR_RailgunScene,clamp(refracted-blur,border,1-border)).rgb*.25;
                return float4(background*alpha,alpha);
            }

            //压缩和稀疏区沿冲击前沿向两侧折射，让地面产生短促的空气震荡。
            float4 Frag(Varyings input) : SV_Target
            {
                if (input.kind > .5) return TrailAir(input);
                float age = saturate(_MuzzleAge/_ShockLife);
                float fade = (1-smoothstep(.12,1,age))*smoothstep(0,.025,_MuzzleAge);
                float2 p = input.uv*2-1;
                float radius = length(p);
                float angle = atan2(p.y,p.x);
                float rim = .76+.014*sin(angle*9+_Seed+age*4)+.009*sin(angle*15-_Seed);
                float section = (radius-rim)/(.075+age*.035);
                float band = exp(-section*section*1.4);
                float alpha = band*fade*.82;
                clip(alpha-.001);
                float2 screen = input.screen.xy/input.screen.w;
                float2 center = input.center.xy/input.center.w;
                float2 pixels = (screen-center)*_ScreenParams.xy;
                float distance = length(pixels);
                float2 direction = pixels/max(distance,1);
                float strength = min(distance/max(radius,.05)*.06,20);
                float pulse = -section*exp(-section*section*.9)*1.6*fade;
                float2 pixel = rcp(_ScreenParams.xy);
                float2 offset = direction*pixel*strength*pulse;
                float2 border = pixel*.5;
                float3 background = tex2D(_SSR_RailgunScene,clamp(screen+offset,border,1-border)).rgb;
                return float4((background+float3(.025,.035,.05))*alpha,alpha);
            }
            ENDCG
        }
    }
}
