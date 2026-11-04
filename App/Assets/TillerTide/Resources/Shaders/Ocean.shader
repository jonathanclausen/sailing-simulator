// Gerstner-wave ocean for URP. Wave parameters come from WaveField (globals), so the boat rides the same surface.
// Colour: deep or shallow water from the region's depth texture, sky reflection by Fresnel, sun glint, fog.
Shader "TillerTide/Ocean"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.03, 0.17, 0.26, 1)
        _ShallowColor ("Shallow water", Color) = (0.14, 0.48, 0.50, 1)
        _RegionTex ("Shallowness (R)", 2D) = "black" {}
        _RegionRect ("Region rect (min x, min z, size x, size z)", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _RegionRect;
            CBUFFER_END
            TEXTURE2D(_RegionTex); SAMPLER(sampler_RegionTex);

            float4 _TT_Waves[4];      // xy direction, z wavelength, w amplitude
            float4 _TT_WaveParams;    // x time, y steepness
            float4 _TT_SkyHorizon;
            float4 _TT_SkyZenith;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fog        : TEXCOORD2;
            };

            void Gerstner(float4 w, float2 p, float t, float steep, float fade, inout float3 disp, inout float3 tangent, inout float3 binormal)
            {
                float k = 6.2831853 / w.z;
                float c = sqrt(9.81 / k);
                float2 d = w.xy;
                float f = k * (dot(d, p) - c * t);
                float a = w.w * fade;
                float q = steep / (k * max(w.w, 1e-4) * 4.0);
                float qa = q * a;
                disp += float3(qa * d.x * cos(f), a * sin(f), qa * d.y * cos(f));
                float wa = k * a;
                float s = sin(f), co = cos(f);
                tangent  += float3(-q * d.x * d.x * wa * s, d.x * wa * co, -q * d.x * d.y * wa * s);
                binormal += float3(-q * d.x * d.y * wa * s, d.y * wa * co, -q * d.y * d.y * wa * s);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float dist = distance(ws.xz, _WorldSpaceCameraPos.xz);
                float fade = saturate(1.0 - dist / 1400.0);
                float t = _TT_WaveParams.x, steep = _TT_WaveParams.y;
                float3 disp = 0, tangent = float3(1, 0, 0), binormal = float3(0, 0, 1);
                [unroll] for (int i = 0; i < 4; i++) Gerstner(_TT_Waves[i], ws.xz, t, steep, fade, disp, tangent, binormal);
                ws += disp;
                o.positionWS = ws;
                o.normalWS = normalize(cross(binormal, tangent));
                o.positionCS = TransformWorldToHClip(ws);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Small ripples on top of the big waves, for sparkle near the boat.
            float2 Ripple(float2 p, float2 d, float len, float amp, float speed, float t)
            {
                float k = 6.2831853 / len;
                return d * (amp * k * cos(dot(d, p) * k - t * speed * k));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _TT_WaveParams.x;
                float2 p = i.positionWS.xz;
                float2 d = normalize(_TT_Waves[0].xy);
                float2 e = float2(-d.y, d.x);
                float dist = distance(_WorldSpaceCameraPos, i.positionWS);
                float near = 1.0 / (1.0 + dist * 0.01);
                float2 g = Ripple(p, d, 2.1, 0.03, 1.8, t) + Ripple(p, normalize(d + e * 0.7), 1.3, 0.02, 1.4, t)
                         + Ripple(p, normalize(d - e * 0.9), 0.7, 0.01, 1.0, t);
                float3 n = normalize(i.normalWS + float3(-g.x, 0, -g.y) * near);

                float2 uv = (p - _RegionRect.xy) / _RegionRect.zw;
                float shallow = SAMPLE_TEXTURE2D(_RegionTex, sampler_RegionTex, uv).r;
                float3 baseCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, shallow);

                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 R = reflect(-V, n);
                float3 sky = lerp(_TT_SkyHorizon.rgb, _TT_SkyZenith.rgb, saturate(R.y * 1.6));
                float fresnel = 0.03 + 0.97 * pow(1.0 - saturate(dot(n, V)), 5.0);

                Light sun = GetMainLight();
                float3 diffuse = baseCol * (0.35 + 0.65 * saturate(dot(n, sun.direction))) * sun.color;
                float3 col = lerp(diffuse, sky, saturate(fresnel));
                float spec = pow(saturate(dot(R, sun.direction)), 220.0) * 3.0;
                col += sun.color * spec;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
