Shader "Decoherence/EnemyHighlight"
{
    // 第二部：把同一批网格在“屏幕空间”向外撑开几像素，只在模板未被遮住的地方画出来
    // —— 结果就是贴在轮廓上、宽度均匀的 2D 描边。
    Properties
    {
        _Color ("Color", Color) = (1, 0.62, 0.2, 0.85)
        _OutlinePixels ("Outline Width (px)", Float) = 2.5
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseDepth ("Pulse Depth", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+45"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        ZTest Always
        Cull Off

        Stencil
        {
            Ref 2
            ReadMask 2
            WriteMask 0
            Comp NotEqual
        }

        Pass
        {
            Name "Outline"

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
                float4 _Color;
                float _OutlinePixels;
                float _PulseSpeed;
                float _PulseDepth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                float4 positionCS = TransformWorldToHClip(positionWS);

                // 法线投到观察空间，取 xy 作为屏幕上的外扩方向；正交相机下 w = 1
                float3 normalVS = TransformWorldToViewDir(normalWS);
                float2 direction = normalVS.xy;
                float magnitude = dot(direction, direction);
                direction = magnitude > 1e-6 ? direction * rsqrt(magnitude) : float2(0.0, 0.0);

                // 像素宽度换算成 NDC：无论远近，描边都是同样粗细
                float2 offset = _OutlinePixels * 2.0 / _ScreenParams.xy;
                positionCS.xy += direction * offset * positionCS.w;

                Varyings output;
                output.positionCS = positionCS;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half pulse = 1.0h - half(_PulseDepth) * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed * 6.2831853h));

                return half4(_Color.rgb, _Color.a * pulse);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
