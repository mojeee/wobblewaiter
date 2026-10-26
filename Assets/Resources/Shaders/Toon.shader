// Cartoon shading with three light bands, plus an ink outline pass that keeps the same width on screen.
Shader "WobbleWaiter/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Outline ("Outline Width", Float) = 0.0032
        _OutlineColor ("Outline Color", Color) = (0.17, 0.11, 0.09, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Toon fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;

        struct Input { float2 uv_MainTex; };

        half4 LightingToon (SurfaceOutput s, half3 lightDir, half atten)
        {
            half l = saturate(dot(s.Normal, lightDir)) * atten;
            half band = l > 0.5 ? 1.0 : (l > 0.12 ? 0.8 : 0.62);
            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * band;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1;
        }
        ENDCG

        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            float _Outline;
            fixed4 _OutlineColor;
            float _WW_OutlineScale;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 off = TransformViewToProjection(n.xy);
                float len = max(length(off), 1e-4);
                o.pos.xy += (off / len) * o.pos.w * _Outline * _WW_OutlineScale;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
