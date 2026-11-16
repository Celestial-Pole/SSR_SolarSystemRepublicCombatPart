Shader "SSR/Missiles/GlowProjection"
{
    Properties { _MainTex ("尾焰影像", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off ZTest LEqual Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            //保留采集图的预乘颜色，在地图光照之后显示自发光尾焰。
            fixed4 frag(v2f_img input) : SV_Target { return tex2D(_MainTex, input.uv); }
            //使用地图平面顶点，不改变烟焰的地图投影位置。
            v2f_img vert(appdata_img input)
            {
                v2f_img output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.texcoord;
                return output;
            }
            ENDCG
        }
    }
}
