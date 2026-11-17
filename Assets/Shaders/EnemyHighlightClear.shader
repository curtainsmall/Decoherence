Shader "Decoherence/EnemyHighlightClear"
{
    // 第三部：把外扩区域上的模板位清零，避免影响下一个敌人的描边
    Properties
    {
        _OutlinePixels ("Outline Width (px)", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+46"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        ColorMask 0
        ZWrite Off
        ZTest Always
        Cull Off

        Stencil
        {
            Ref 0
            ReadMask 2
            WriteMask 2
            Comp Always
            Pass Replace
        }

        Pass
        {
            Name "Clear"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float _OutlinePixels;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                float4 positionCS = TransformWorldToHClip(positionWS);

                float3 normalVS = TransformWorldToViewDir(normalWS);
                float2 direction = normalVS.xy;
                float magnitude = dot(direction, direction);
                direction = magnitude > 1e-6 ? direction * rsqrt(magnitude) : float2(0.0, 0.0);

                // 与描边同样的外扩，保证清掉的正是描边画过的区域
                float2 offset = _OutlinePixels * 2.0 / _ScreenParams.xy;
                positionCS.xy += direction * offset * positionCS.w;

                Varyings output;
                output.positionCS = positionCS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
