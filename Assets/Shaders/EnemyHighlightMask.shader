Shader "Decoherence/EnemyHighlightMask"
{
    // 第一部：把敌人的投影区域写进模板缓冲，不产生颜色
    Properties
    {
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+44"
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
            Ref 2
            ReadMask 2
            WriteMask 2
            Comp Always
            Pass Replace
        }

        Pass
        {
            Name "Mask"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

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
