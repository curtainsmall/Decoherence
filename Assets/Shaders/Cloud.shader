Shader "Decoherence/Cloud"
{
    Properties
    {
        _Color ("Color", Color) = (0.80, 0.92, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.55
        _EdgeSoftness ("Edge Softness (fraction of radius)", Range(0.02, 0.6)) = 0.18
        _NoiseScale ("Noise Scale", Float) = 4.5
        _NoiseRotation ("Noise Rotation", Float) = 0
        _NoiseStrength ("Edge Raggedness", Range(0, 1.2)) = 0.85
        _WispStrength ("Interior Wispiness", Range(0, 1)) = 0.45
        _DriftSpeed ("Drift Speed", Float) = 0.35
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.45
        _Dissolve ("Dissolve", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
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
            Name "Cloud"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Alpha;
                float _EdgeSoftness;
                float _NoiseScale;
                float _NoiseRotation;
                float _NoiseStrength;
                float _WispStrength;
                float _DriftSpeed;
                float _RimPower;
                float _RimStrength;
                float _Dissolve;
            CBUFFER_END

            float hash13(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.11, 0.27, 0.43));
                p *= 19.19;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float valueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash13(i);
                float n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0));
                float n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1));
                float n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1));
                float n111 = hash13(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);

                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            float fbm(float3 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                float normalization = 0.0;

                for (int i = 0; i < 3; i++)
                {
                    sum += valueNoise(p) * amplitude;
                    normalization += amplitude;
                    p *= 2.07;
                    amplitude *= 0.5;
                }

                return sum / max(normalization, 1e-4);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 samplePos = input.positionOS * _NoiseScale;

                float c = cos(_NoiseRotation);
                float s = sin(_NoiseRotation);
                samplePos.xz = float2(c * samplePos.x - s * samplePos.z, s * samplePos.x + c * samplePos.z);

                samplePos += float3(_DriftSpeed * _Time.y * 0.35, _DriftSpeed * _Time.y * 0.21, _DriftSpeed * _Time.y * 0.17);

                float radius = length(input.positionOS);
                float edge = saturate((1.0 - radius) / max(_EdgeSoftness, 0.001));

                float coarse = fbm(samplePos);
                float fine = fbm(samplePos * 2.7 + 17.3);

                // 外缘破碎
                float ragged = edge + (coarse - 0.5) * _NoiseStrength;
                float alpha = saturate(ragged * 2.2);

                // 内部絮状结构：不改变边界位置，只让密度起伏
                float wisps = lerp(1.0, saturate(0.35 + fine * 1.1), _WispStrength);
                alpha *= wisps;

                // 溶解
                float dissolveMask = saturate((coarse - _Dissolve) * 4.0 + 0.5);
                alpha = saturate(alpha * dissolveMask * _Alpha);

                if (alpha <= 0.004)
                {
                    discard;
                }

                float rim = pow(saturate(radius), _RimPower) * _RimStrength;
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                float facing = saturate(dot(normalize(input.normalWS), viewDir));
                float softRim = (1.0 - facing) * rim * (0.6 + coarse * 0.8);

                float3 color = _Color.rgb + softRim * float3(0.7, 0.86, 1.0);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
