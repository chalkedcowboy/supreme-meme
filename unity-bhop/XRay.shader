// Fresnel silhouette drawn only where the mesh is hidden behind other geometry.
// Used by XRayVision / XRayTarget. Works in the Built-in pipeline and URP.
Shader "Custom/XRay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.3, 0.2, 0.8)
        _Fill ("Fill Opacity", Range(0, 1)) = 0.25
        _RimPower ("Rim Power", Range(0.5, 8)) = 2
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (Greater = only behind walls)", Float) = 5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+100" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite Off
            ZTest [_ZTest]
            Blend SrcAlpha One
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Fill;
            half _RimPower;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half rim = 1 - saturate(dot(normalize(i.normal), normalize(i.viewDir)));
                half alpha = lerp(_Fill, 1, pow(rim, _RimPower)) * _Color.a;
                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }
}
