Shader "SSR/Outline/MapPlane"
{
    Properties { _MainTex ("炮塔离屏纹理", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off ZWrite On ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Fragment
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            //预乘颜色只混合一次，透明背景不写深度，光照由地图上方的覆盖层调制。
            fixed4 Fragment(v2f_img input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv);
                clip(color.a - 0.001);
                return color;
            }
            ENDCG
        }
    }
}
