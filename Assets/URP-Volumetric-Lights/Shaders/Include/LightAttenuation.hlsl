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
half3 GetMainLightColor(half3 color, float3 worldPosition)
{    
    float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);

    #if defined(SHADOWS_ENABLED)
        color *= MainLightRealtimeShadow(shadowCoord);
    #endif

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleMainLightCookie(worldPosition);
        color *= cookieColor;
    #endif

    return color;
}


half3 GetLightAttenuation(float3 worldPosition, int additionalLightIndex, out float3 lightDirection)
{
    VolumeLight light = GetAdditionalLight(additionalLightIndex);

    lightDirection = light.position.xyz - worldPosition * light.position.w;

    if (light.shadowIndex < 0)
    {
        return GetMainLightColor(light.color, worldPosition);
    }

    float distanceSqr = max(sqrlen(lightDirection), HALF_MIN);

    // A reciprocal square root in a nested shader loop is kind of scary... But it doesn't seem to hurt performance too much
    float rsqr = rsqrt(distanceSqr);

    half3 color = light.color;

    lightDirection = lightDirection * rsqr;
    half distanceAttenuation = DistanceAttenuation(distanceSqr, light.attenuation.xy) * AngleAttenuation(light.spotDirection.xyz, lightDirection, light.attenuation.zw);
    color *= distanceAttenuation;

    #if defined(SHADOWS_ENABLED)
        float shadowAttenuation = AdditionalLightRealtimeShadow(light.shadowIndex, worldPosition, lightDirection);
        color *= shadowAttenuation;
    #endif

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleAdditionalLightCookie(light.shadowIndex, worldPosition);
        color *= cookieColor;
    #endif

    return color;
}