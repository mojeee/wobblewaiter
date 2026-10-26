// Cheap mobile post effects: soft bloom on bright spots, a warm color grade and a gentle vignette.
Shader "WobbleWaiter/PostFx"
{
    Properties { _MainTex ("Source", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom;
    float4 _MainTex_TexelSize;
    float _Threshold, _Intensity, _Vignette, _Saturation, _Contrast;
    float3 _Tint;

    half3 Box(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: keep only the bright parts, while shrinking
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target
            {
                half3 c = Box(i.uv, 1);
                half b = max(c.r, max(c.g, c.b));
                half soft = saturate((b - _Threshold + 0.25) / 0.5);
                half k = max(soft * soft * 0.5, b - _Threshold) / max(b, 1e-4);
                return half4(c * saturate(k), 1);
            }
            ENDCG
        }
        // 1: blur while shrinking
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target { return half4(Box(i.uv, 1), 1); }
            ENDCG
        }
        // 2: blur while growing, added on top of the bigger level
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
            ENDCG
        }
        // 3: final picture
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag (v2f_img i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb + tex2D(_Bloom, i.uv).rgb * _Intensity;
                c *= _Tint;
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                float2 d = i.uv - 0.5;
                c *= 1 - dot(d, d) * _Vignette;
                return half4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
