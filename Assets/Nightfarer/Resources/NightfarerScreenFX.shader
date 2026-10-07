// Full-screen combat effects for Subject 143 (drawn by ScreenFX after post-processing):
// radial zoom blur toward a point, anime speed lines, and impact frames (high-contrast black/white,
// inverted, with a coloured flash), all driven by material properties.
Shader "Hidden/Nightfarer/ScreenFX"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "NightfarerScreenFX"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float4 _FXCenter;     // xy: blur / lines centre in UV
            float _FXBlur;        // radial blur length (fraction of the distance to the centre)
            float _FXLines;       // speed line strength 0..1
            float _FXMono;        // impact frame: threshold to black / white 0..1
            float _FXInvert;      // impact frame: invert 0..1
            float _FXFlash;       // flash toward _FXTint 0..1
            float4 _FXTint;
            float _FXSeed;

            float Hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 d = uv - _FXCenter.xy;

                half3 col = 0;
                const int N = 14;
                [unroll] for (int i = 0; i < N; i++)
                {
                    float t = i / (float)(N - 1);
                    col += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - d * (_FXBlur * t)).rgb;
                }
                col /= N;

                // Speed lines: thin radial wedges at random angles, flickering in steps (drawn on 2s).
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 da = float2(d.x * aspect, d.y);
                float r = length(da);
                float ang = atan2(da.y, da.x) / 6.2831853 + 0.5;
                float bins = 220.0;
                float id = floor(ang * bins);
                float f = frac(ang * bins);
                float seed = _FXSeed;
                float on = step(0.72, Hash(id + seed * 17.0));
                float inner = 0.28 + 0.35 * Hash(id * 3.1 + seed);
                float width = 0.15 + 0.35 * Hash(id * 7.7 + seed);
                float streak = on * smoothstep(inner, inner + 0.25, r) * saturate(1.0 - abs(f - 0.5) * 2.0 / width);
                col = lerp(col, half3(1, 1, 1), saturate(_FXLines * streak));

                // Impact frames.
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                half3 mono = lum > 0.24 ? half3(1, 1, 1) : half3(0, 0, 0);
                col = lerp(col, mono, _FXMono);
                col = lerp(col, 1.0 - col, _FXInvert);
                col = lerp(col, _FXTint.rgb * max(col, 0.35), _FXFlash);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
