Shader "Decoherence/ArenaWall"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.231, 0.250, 0.283, 1)
        _SeamColor ("Seam Color", Color) = (0.127, 0.141, 0.162, 1)
        _SeamSpacing ("Seam Spacing", Float) = 2
        _SeamWidth ("Seam Half Width", Float) = 0.006
        _JointHeight ("Joint Height", Float) = 1.2
        _JointWidth ("Joint Half Width", Float) = 0.006
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex ArenaVertex
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Assets/Shaders/ArenaLit.hlsl"
            #include "Assets/Shaders/ArenaGrid.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SeamColor;
                float _SeamSpacing;
                float _SeamWidth;
                float _JointHeight;
                float _JointWidth;
            CBUFFER_END

            half4 frag(ArenaVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // 板缝沿墙的水平方向排布：法线朝 X 的墙用 z，朝 Z 的墙用 x；墙顶不画缝
                float2 plane = abs(normalWS.x) > abs(normalWS.z)
                    ? float2(input.positionWS.z, input.positionWS.y)
                    : float2(input.positionWS.x, input.positionWS.y);

                float pixel = max(fwidth(plane.x), fwidth(plane.y));
                float seam = LineMask(RepeatDistance(plane.x, _SeamSpacing), _SeamWidth, pixel);
                float joint = LineMask(abs(plane.y - _JointHeight), _JointWidth, pixel);
                float panels = abs(normalWS.y) < 0.5 ? max(seam, joint) : 0.0;

                half3 albedo = lerp(_BaseColor.rgb, _SeamColor.rgb, panels);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                return half4(ArenaLight(input, albedo, shadowCoord), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // 与 ForwardLit 保持一致，着色器才兼容 SRP Batcher
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SeamColor;
                float _SeamSpacing;
                float _SeamWidth;
                float _JointHeight;
                float _JointWidth;
            CBUFFER_END
            ENDHLSL
        }
    }

    Fallback Off
}
