#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"


int _LightCount;

int _LightToShadowIndices[MAX_LIGHT_COUNT];  
float4 _LightPositions[MAX_LIGHT_COUNT];      
half3 _LightColors[MAX_LIGHT_COUNT];          
half4 _LightAttenuations[MAX_LIGHT_COUNT];    
half4 _SpotDirections[MAX_LIGHT_COUNT];       


struct VolumeLight
{
    int shadowIndex;
    float4 position;
    half3 color;
    half4 attenuation;
    half4 spotDirection;
};


VolumeLight GetAdditionalLight(int index)
{
    VolumeLight output;

    output.shadowIndex = _LightToShadowIndices[index];
    output.position = _LightPositions[index];
    output.color = _LightColors[index];
    output.attenuation = _LightAttenuations[index];
    output.spotDirection = _SpotDirections[index];

    return output;
}


// TODO: Find out how to get baked lights to work with this- if I discover what to pass into the occlusion probe channels 
half3 GetMainColorAndAttenuation(half3 color, float3 worldPosition, out float attenuation)
{    
    float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);

    attenuation = 1;

    #if defined(SHADOWS_ENABLED)
        float shadAttenuation = MainLightRealtimeShadow(shadowCoord);
        color *= shadAttenuation;
        attenuation *= shadAttenuation;
    #endif

    #if defined(_LIGHT_COOKIES)
        color *= SampleMainLightCookie(worldPosition);
    #endif

    return color;
}


// Light color is stored in xyz, and attenuation is stored in w.
half3 GetColorAndAttenuation(float3 worldPosition, int additionalLightIndex, out float3 lightDirection, out float attenuation)
{
    VolumeLight light = GetAdditionalLight(additionalLightIndex);

    lightDirection = light.position.xyz - worldPosition * light.position.w;

    if (light.shadowIndex < 0)
        return GetMainColorAndAttenuation(light.color, worldPosition, attenuation);

    float distanceSqr = max(sqrlen(lightDirection), HALF_MIN);

    // A reciprocal square root in a nested shader loop is kind of scary... but it doesn't seem to hurt performance too much
    float rsqr = rsqrt(distanceSqr);

    half3 color = light.color;
    lightDirection = lightDirection * rsqr;

    float distAttenuation = DistanceAttenuation(distanceSqr, light.attenuation.xy) * AngleAttenuation(light.spotDirection.xyz, lightDirection, light.attenuation.zw);
    attenuation = distAttenuation;

    color *= distAttenuation;

    #if defined(SHADOWS_ENABLED)
        float shadAttenuation = AdditionalLightRealtimeShadow(light.shadowIndex, worldPosition, lightDirection);
        color *= shadAttenuation;
        attenuation *= shadAttenuation;
    #endif

    #if defined(_LIGHT_COOKIES)
        color *= SampleAdditionalLightCookie(light.shadowIndex, worldPosition);
    #endif

    return color;
}