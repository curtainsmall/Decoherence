#ifndef DECOHERENCE_ARENA_LIT_INCLUDED
#define DECOHERENCE_ARENA_LIT_INCLUDED

// 场地（地面、设施墙、围栏）共用的最小前向光照：一盏主光 + 附加光源 + SH 环境光，不含高光。
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

struct ArenaAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
};

struct ArenaVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
};

ArenaVaryings ArenaVertex(ArenaAttributes input)
{
    VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

    ArenaVaryings output;
    output.positionCS = positions.positionCS;
    output.positionWS = positions.positionWS;
    output.normalWS = normals.normalWS;

    return output;
}

half3 ArenaLight(ArenaVaryings input, half3 albedo, float4 shadowCoord)
{
    float3 normalWS = normalize(input.normalWS);

    Light mainLight = GetMainLight(shadowCoord);
    half mainAttenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    half3 light = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * mainAttenuation);

#if defined(_ADDITIONAL_LIGHTS)
    // 集群（Forward+）路径的循环宏要用到 inputData 的屏幕坐标与位置
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    uint lightCount = GetAdditionalLightsCount();

    LIGHT_LOOP_BEGIN(lightCount)
        Light additional = GetAdditionalLight(lightIndex, input.positionWS);
        half additionalAttenuation = additional.distanceAttenuation * additional.shadowAttenuation;

        light += additional.color * (saturate(dot(normalWS, additional.direction)) * additionalAttenuation);
    LIGHT_LOOP_END
#endif

    half3 ambient = SampleSHPixel(half3(0.0, 0.0, 0.0), normalWS);

    return albedo * (light + ambient);
}

#endif
