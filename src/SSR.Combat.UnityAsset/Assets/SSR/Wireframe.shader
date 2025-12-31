Shader "Unlit/Wireframe"
{
    Properties
    {
        _OutlineWidth ("Outline Width", Float) = 0.015625
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent-100"}
        LOD 100
        Cull Off
        ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 orginalVert : TEXCOORD1;
                float4 transposVert : TEXCOORD2;
                float3 cameraDir : TEXCOORD3;
                float3 normal : TEXCOORD4;
                // float3 from : TEXCOORD5;
                // float3 to : TEXCOORD6;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float _OutlineWidth;
            sampler2D _DepthTex;
            float4 _DepthTex_ST;


            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = float4(UnityObjectToViewPos(v.vertex),1);
                o.orginalVert = o.vertex;
                o.transposVert = o.vertex;
                float4 vert = UnityViewToClipPos(o.vertex);
                float4 screenNear = UnityViewToClipPos(float3(0,0,-_ProjectionParams.y));
                screenNear.xy = vert.xy * screenNear.w / vert.w;
                screenNear = mul(unity_CameraInvProjection, screenNear);
                float4 screenFar = UnityViewToClipPos(float3(0,0,-_ProjectionParams.z));
                screenFar.xy = vert.xy * screenFar.w / vert.w;
                screenFar = mul(unity_CameraInvProjection, screenFar);
                o.cameraDir = normalize(screenFar.xyz - screenNear.xyz);
                o.normal = o.cameraDir;
                // o.from = o.vertex;
                // o.to = o.vertex;
                return o;
            }

            
            void convetLine(v2f v1, v2f v2, inout TriangleStream<v2f> outStream)
            {
                float3 dirT = normalize(v1.vertex.xyz - v2.vertex.xyz);
                float3 dirR = dirT.yxz;
                dirR.y = -dirR.y;
                float3 norm = normalize(cross(dirT,dirR));
                dirR.z = 0;
                dirT.z = 0;
                

                v2f lt1 = v1;
                v2f rt1 = v1;
                v2f lt2 = v1;
                v2f rt2 = v1;

                v2f lb2 = v2;
                v2f rb2 = v2;

                lt1.vertex.xyz += dirT * _OutlineWidth;
                rt1.vertex.xyz += dirT * _OutlineWidth;

                lt1.vertex.xyz -= dirR * _OutlineWidth;
                rt1.vertex.xyz += dirR * _OutlineWidth;
                lt2.vertex.xyz -= dirR * _OutlineWidth;
                rt2.vertex.xyz += dirR * _OutlineWidth;
                lb2.vertex.xyz -= dirR * _OutlineWidth;
                rb2.vertex.xyz += dirR * _OutlineWidth;
                lt1.transposVert = lt1.vertex;
                rt1.transposVert = rt1.vertex;
                lt2.transposVert = lt2.vertex;
                rt2.transposVert = rt2.vertex;
                lb2.transposVert = lb2.vertex;
                rb2.transposVert = rb2.vertex;
                lt1.vertex = UnityViewToClipPos(lt1.vertex);
                rt1.vertex = UnityViewToClipPos(rt1.vertex);
                lt2.vertex = UnityViewToClipPos(lt2.vertex);
                rt2.vertex = UnityViewToClipPos(rt2.vertex);
                lb2.vertex = UnityViewToClipPos(lb2.vertex);
                rb2.vertex = UnityViewToClipPos(rb2.vertex);
                
                //spot
                outStream.Append(lt1);
                outStream.Append(rt1);
                outStream.Append(lt2);
                outStream.RestartStrip();
                outStream.Append(rt1);
                outStream.Append(lt2);
                outStream.Append(rt2);
                outStream.RestartStrip();
                //line
                lt2.normal = norm;
                rt2.normal = norm;
                lb2.normal = norm;
                rb2.normal = norm;
                // lb2.from = lt2.from;
                // rb2.from = rt2.from;
                // lt2.to = lb2.to;
                // rt2.to = rb2.to;
                outStream.Append(lt2);
                outStream.Append(rt2);
                outStream.Append(lb2);
                outStream.RestartStrip();
                outStream.Append(rt2);
                outStream.Append(lb2);
                outStream.Append(rb2);
                outStream.RestartStrip();
                // outStream.Append(lb1);
                // outStream.Append(rb1);
                // outStream.Append(lb2);
                // outStream.RestartStrip();
                // outStream.Append(rb1);
                // outStream.Append(lb2);
                // outStream.Append(rb2);
                // outStream.RestartStrip();
            } 

            [maxvertexcount(36)] 
            void geom(triangle v2f input[3],inout TriangleStream<v2f> outStream)
            {
                // float3 dirA = normalize(input[0].vertex.xyz - input[1].vertex.xyz);
                // float3 dirA = normalize(input[1].vertex.xyz - input[2].vertex.xyz);
                // float3 dirB = normalize(input[2].vertex.xyz - input[0].vertex.xyz);
                // float3 s = float3(
                //     abs(dot(dirA,dirA)) + abs(dot(dirA,dirB)),
                //     abs(dot(dirA,dirA)) + abs(dot(dirA,dirB)),
                //     abs(dot(dirA,dirB)) + abs(dot(dirA,dirB))
                //     );
                // float m = max(s.x,max(s.y,s.z));
                // if(m != s.x) convetLine(input[0],input[1],outStream);
                // if(m != s.y) convetLine(input[1],input[2],outStream);
                // if(m != s.z) convetLine(input[2],input[0],outStream);
                convetLine(input[0],input[1],outStream);
                convetLine(input[1],input[2],outStream);
                convetLine(input[2],input[0],outStream);
            } 

            fixed4 frag (v2f i, out float depth : SV_DEPTH) : SV_Target
            {
                // sample the texture
                UNITY_SETUP_INSTANCE_ID(i);
                if(_OutlineWidth <= 0) discard;
                float3 dir = i.transposVert.xyz-i.orginalVert.xyz;
                float disSq = dot(dir,dir);
                float outlineWidthSq = _OutlineWidth * _OutlineWidth;
                if(outlineWidthSq <= disSq) discard;
                disSq = outlineWidthSq - disSq;
                float dis = sqrt(disSq) / dot(i.normal, i.cameraDir);
                // float dis = 0;
                i.vertex = UnityViewToClipPos(i.transposVert.xyz - dis * i.cameraDir.xyz);

                depth = max(i.vertex.z, 0) / i.vertex.w;




                // apply fog
                return fixed4(0,0,0,1);
                // return float4(dis.xxx,1);
            }
            ENDCG
        }

    }
}
