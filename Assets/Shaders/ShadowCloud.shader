Shader "Decoherence/ShadowCloud"
{
    Properties
    {
        _Color ("Color", Color) = (0.010, 0.012, 0.018, 1)
        _Alpha ("Alpha", Range(0, 1)) = 1.0
        _NoiseScale ("Noise Scale", Float) = 8
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.5
        _FalloffPower ("Puff Falloff Power", Range(0.3, 4)) = 1.5
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
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Alpha;
                float _NoiseScale;
                float _NoiseStrength;
                float _FalloffPower;
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
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 每个 puff 是一张面向相机的四边形，UV 决定柔边
                float radius = saturate(length(input.uv - 0.5) * 2.0);
                float soft = pow(saturate(1.0 - radius), _FalloffPower);

                // 噪声域向上漂移，边缘被侵蚀成丝缕
                float2 domain = input.positionWS.xz * _NoiseScale
                    + float2(_Time.y * 0.08, -_Time.y * _RiseSpeed);
                float drift = input.positionWS.y * _NoiseScale * 0.6 - _Time.y * _RiseSpeed;

                float n1 = fbm(domain + drift);
                float n2 = fbm(domain * 2.6 - drift * 1.2 + 19.0);

                float grain = lerp(1.0, saturate(n1 * 1.25 + n2 * 0.55), _NoiseStrength);

                float alpha = saturate(input.color.a * _Alpha * soft * grain);

                if (alpha <= 0.004)
                {
                    discard;
                }

                float3 color = _Color.rgb * (0.8 + n1 * 0.55);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
