Shader "Decoherence/ShadowCloud"
{
    Properties
    {
        _Color ("Color", Color) = (0.010, 0.012, 0.018, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.45
        _EdgeFeather ("Top Fade", Range(0.05, 1)) = 0.55
        _FalloffPower ("Radial Falloff Power", Range(0.5, 6)) = 1.5
        _EdgeAlpha ("Edge Alpha", Range(0, 1)) = 0.45
        _CoreShift ("Core Darken", Range(0, 1)) = 0.45
        _RiseSpeed ("Rise Speed", Float) = 0.55
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
                float _FalloffPower;
                float _EdgeAlpha;
                float _CoreShift;
                float _RiseSpeed;
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
                // color.r = 高度比例（0 底 / 1 顶），color.g = 径向比例（0 核心 / 1 边界）
                float height = saturate(input.color.r);
                float radial = saturate(input.color.g);
                float density = input.color.a;

                // 非线性径向过渡：核心实、边缘快速变淡
                float core = pow(saturate(1.0 - radial), _FalloffPower);
                float radialMask = lerp(_EdgeAlpha, 1.0, core);

                // 噪声域整体向上漂移，观感像升腾的烟
                float2 domain = input.positionWS.xz * _NoiseScale
                    + float2(_Time.y * 0.08, -_Time.y * _RiseSpeed);
                float drift = input.positionWS.y * _NoiseScale * 0.6 - _Time.y * _RiseSpeed;

                float n1 = fbm(domain + drift);
                float n2 = fbm(domain * 2.6 - drift * 1.2 + 19.0);

                // 边缘靠噪声打出破洞，核心几乎不受影响
                float grain = lerp(1.0, saturate(n1 * 1.15 + n2 * 0.5), _NoiseStrength * (1.0 - core * 0.8));

                float alpha = saturate(density * _Alpha * radialMask * (1.0 + core * 0.8) * grain);

                // 顶部收口再淡一点，避免露出硬边
                alpha *= smoothstep(1.0, lerp(0.88, 0.97, _EdgeFeather), height);

                if (alpha <= 0.004)
                {
                    discard;
                }

                // 核心更暗：alpha 更高、颜色更沉
                float3 color = _Color.rgb * (1.0 - core * _CoreShift) * (0.85 + n1 * 0.5);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
