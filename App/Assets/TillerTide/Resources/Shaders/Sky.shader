// Sky dome gradient with a sun disc, drawn behind everything.
Shader "TillerTide/Sky"
{
    SubShader
    {
        Tags { "RenderType" = "Background" "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Cull Front ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float4 _TT_SkyHorizon;
            float4 _TT_SkyZenith;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = normalize(v.positionOS.xyz);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float h = saturate(i.dir.y);
                float3 col = lerp(_TT_SkyHorizon.rgb, _TT_SkyZenith.rgb, pow(h, 0.5));
                Light sun = GetMainLight();
                float s = saturate(dot(normalize(i.dir), sun.direction));
                col += sun.color * (pow(s, 600.0) * 4.0 + pow(s, 12.0) * 0.18);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
