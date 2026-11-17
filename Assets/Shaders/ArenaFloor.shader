Shader "Decoherence/ArenaFloor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.150, 0.165, 0.192, 1)
        _MinorColor ("Minor Line Color", Color) = (0.120, 0.133, 0.159, 1)
        _MajorColor ("Major Line Color", Color) = (0.072, 0.080, 0.098, 1)
        _MinorSpacing ("Minor Spacing", Float) = 0.5
        _MajorSpacing ("Major Spacing", Float) = 2
        _MinorWidth ("Minor Line Half Width", Float) = 0.008
        _MajorWidth ("Major Line Half Width", Float) = 0.012
        _GlowColor ("Grid Glow Color", Color) = (0.18, 0.55, 0.75, 1)
        _GlowStrength ("Grid Glow Strength", Float) = 0.35
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
                half4 _MinorColor;
                half4 _MajorColor;
                float _MinorSpacing;
                float _MajorSpacing;
                float _MinorWidth;
                float _MajorWidth;
                half4 _GlowColor;
                float _GlowStrength;
            CBUFFER_END

            half4 frag(ArenaVaryings input) : SV_Target
            {
                // 网格按世界坐标铺开，所以平面缩放不会改变格距
                float2 plane = input.positionWS.xz;
                float2 footprint = fwidth(plane);
                float pixel = max(footprint.x, footprint.y);

                float minor = LineMask(min(RepeatDistance(plane.x, _MinorSpacing), RepeatDistance(plane.y, _MinorSpacing)), _MinorWidth, pixel);
                float major = LineMask(min(RepeatDistance(plane.x, _MajorSpacing), RepeatDistance(plane.y, _MajorSpacing)), _MajorWidth, pixel);

                half3 albedo = lerp(_BaseColor.rgb, _MinorColor.rgb, minor);
                albedo = lerp(albedo, _MajorColor.rgb, major);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                // 主格线是嵌在地里的自发光条
                half3 emissive = _GlowColor.rgb * (_GlowStrength * major);

                return half4(ArenaLight(input, albedo, shadowCoord) + emissive, 1);
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
                half4 _MinorColor;
                half4 _MajorColor;
                float _MinorSpacing;
                float _MajorSpacing;
                float _MinorWidth;
                float _MajorWidth;
                half4 _GlowColor;
                float _GlowStrength;
            CBUFFER_END
            ENDHLSL
        }
    }

    Fallback Off
}
