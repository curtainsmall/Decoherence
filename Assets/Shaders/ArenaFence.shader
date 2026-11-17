Shader "Decoherence/ArenaFence"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.020, 0.024, 0.028, 1)
        _CellSize ("Mesh Cell Size", Float) = 0.12
        _MeshWidth ("Mesh Line Half Width", Float) = 0.012
        _PostSpacing ("Post Spacing", Float) = 4
        _PostWidth ("Post Half Width", Float) = 0.05
        _RailHeight ("Top Rail Height", Float) = 2.84
        _RailWidth ("Top Rail Half Width", Float) = 0.035
        _Opacity ("Opacity", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Back

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
                float _CellSize;
                float _MeshWidth;
                float _PostSpacing;
                float _PostWidth;
                float _RailHeight;
                float _RailWidth;
                float _Opacity;
            CBUFFER_END

            half4 frag(ArenaVaryings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // 面板是竖板：法线朝 X 时用 z 作水平轴，朝 Z 时用 x
                float2 plane = abs(normalWS.x) > abs(normalWS.z)
                    ? float2(input.positionWS.z, input.positionWS.y)
                    : float2(input.positionWS.x, input.positionWS.y);

                float pixel = max(fwidth(plane.x), fwidth(plane.y));

                // 两族 45° 斜线交织成菱形网眼
                float mesh = LineCoverage(
                    min(RepeatDistance(plane.x + plane.y, _CellSize), RepeatDistance(plane.x - plane.y, _CellSize)),
                    _MeshWidth,
                    pixel);

                float post = LineMask(RepeatDistance(plane.x, _PostSpacing), _PostWidth, pixel);
                float rail = LineMask(abs(plane.y - _RailHeight), _RailWidth, pixel);
                float coverage = saturate(mesh + max(post, rail));

                if (coverage <= 0.01)
                {
                    discard;
                }

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half3 color = ArenaLight(input, _BaseColor.rgb, shadowCoord);

                return half4(color, coverage * _Opacity);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
