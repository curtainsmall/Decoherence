Shader "Decoherence/ShadowCloud"
{
    Properties
    {
        _Color ("Color", Color) = (0.04, 0.05, 0.09, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.72
        _NoiseScale ("Noise Scale", Float) = 9
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.35
        _EdgeFeather ("Edge Feather", Range(0.05, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+60"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "ShadowCloud"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Alpha;
                float _NoiseScale;
                float _NoiseStrength;
                float _EdgeFeather;
            CBUFFER_END

            float hash12(float2 p)
            {
                p = frac(p * float2(0.3183, 0.2718));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash12(i);
                float b = hash12(i + float2(1, 0));
                float c = hash12(i + float2(0, 1));
                float d = hash12(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                float normalization = 0.0;

                for (int i = 0; i < 3; i++)
                {
                    sum += valueNoise(p) * amplitude;
                    normalization += amplitude;
                    p *= 2.11;
                    amplitude *= 0.5;
                }

                return sum / max(normalization, 1e-4);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float height = saturate(input.color.r);

                // 同一 (x,z) 在不同高度取到不同图案，云才有体积感
                float2 domain = input.positionWS.xz * _NoiseScale
                    + float2(_Time.y * 0.12, -_Time.y * 0.09);

                float lowNoise = fbm(domain + input.positionWS.y * 1.7);
                float highNoise = fbm(domain * 2.3 - input.positionWS.y * 1.1 + 17.0);

                float density = input.color.a;
                float grain = lerp(1.0, lowNoise * 0.9 + highNoise * 0.35, _NoiseStrength);
                float alpha = saturate(density * _Alpha * grain);

                // 顶部收口处再淡一点，避免露出硬边
                float fadeStart = lerp(0.78, 0.9, _EdgeFeather);
                alpha *= smoothstep(1.05, fadeStart, height);

                if (alpha <= 0.004)
                {
                    discard;
                }

                float3 color = _Color.rgb * (0.75 + lowNoise * 0.55);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
