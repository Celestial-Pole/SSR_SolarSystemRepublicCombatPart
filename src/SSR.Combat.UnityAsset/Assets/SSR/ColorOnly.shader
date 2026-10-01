Shader "Unlit/ColorOnly"
{
    Properties
    {
        _Color ("模型底色", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            //只接收几何位置，避免顶点颜色或法线改变模型底色。
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float worldHeight : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _Color;
            float _ClipGround;

            //投影实体顶点，保留正常的深度遮挡关系。
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldHeight = mul(unity_ObjectToWorld, v.vertex).y;
                return o;
            }

            //仅输出不透明底色，所有黑色描边由独立图像合成阶段生成。
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (_ClipGround > 0.5) clip(i.worldHeight);
                return fixed4(_Color.rgb, 1);
            }
            ENDCG
        }
    }
}
