Shader "Unlit/ColorOnly"
{
    Properties
    {
        [Toggle(ENABLE_REL2ANIM)] _Rel2Anim ("Rel to Anim", Float) = 1
        _Color ("Color", Color) = (1,1,1,1) 
        _LightInModedlSpace ("Light In Model Space", Vector) = (0,0,0,0) 
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent-100"}
        LOD 100
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature ENABLE_REL2ANIM
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float3 normal : TEXCOORD1;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _Color;
            float3 _LightInModedlSpace;

            float Rel2AnimShadow(float rel)
            {
                rel = clamp(rel, 0.0, 1.0);
                #ifdef ENABLE_REL2ANIM
                return ceil(rel * 1.5) * 0.25 + 0.5;
                #else
                return rel * 0.5 + 0.5;
                #endif
            }

            float3 LightInModedlSpace()
            {
                float l = length(_LightInModedlSpace);
                if(l < 0.001) return float3(0,0,1);
                else return _LightInModedlSpace / l;
            }


            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                float l = length(v.normal);
                if(l < 0.001) v.normal = LightInModedlSpace();
                else v.normal /= l;
                o.normal = v.normal;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                UNITY_SETUP_INSTANCE_ID(i);

                float4 outColor = float4(_Color.rgb * Rel2AnimShadow(dot(i.normal, LightInModedlSpace())), _Color.a);
                // apply fog
                return outColor;
            }
            ENDCG
        }
    }
}
