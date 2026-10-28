Shader "SSR/Turrets/BakedColor"
{
    Properties
    {
        _MainTex ("颜色贴图", 2D) = "white" {}
        _Color ("颜色乘数", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            //保留纹理坐标及实例化输入。
            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            //传递地面裁剪高度，光照仍由公共离屏管线计算。
            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float worldHeight : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ClipGround;

            //投影烘焙后的蒙皮网格。
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.worldHeight = mul(unity_ObjectToWorld, input.vertex).y;
                return output;
            }

            //只读取底色，避免把灯光或描边重复烘进模型。
            fixed4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_ClipGround > 0.5) clip(input.worldHeight);
                return fixed4(tex2D(_MainTex, input.uv).rgb * _Color.rgb, 1);
            }
            ENDCG
        }
    }
}
