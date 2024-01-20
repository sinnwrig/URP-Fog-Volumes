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


// TODO: FInd out how to get baked lights to work with this- once I find out what to pass into the occlusion probe channels 
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

    float distanceSqr = max(dot(lightDirection, lightDirection), HALF_MIN);

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


// Original from https://github.com/SlightlyMad/VolumetricLights/blob/master/Assets/Shaders/VolumetricLight.shader
float MiePhase(float cosAngle, float mieG)
{
	float gSqr = mieG * mieG;

	// Magic number is 1/4pi
    return (0.07957747154) * ((1 - gSqr) / (pow(abs((1 + gSqr) - (2 * mieG) * cosAngle), 1.5)));
}


half3 GetLightAttenuation(float3 worldPosition)
{
	half3 lightCol = 0.0;

    #if defined(LIGHTING_ENABLED)
        for (int i = 0; i < min(_LightCount, MAX_LIGHT_COUNT); i++)
        {
            float3 direction;
            lightCol += GetLightAttenuation(worldPosition, i, direction);
        }
    #endif

	return lightCol;
}


half3 GetLightAttenuationMie(float3 worldPosition, float3 direction, float mieG)
{
    half3 lightCol = 0.0;

    #if defined(LIGHTING_ENABLED)
        for (int i = 0; i < min(_LightCount, MAX_LIGHT_COUNT); i++)
        {
            float3 lightDir;
            half3 lightColor = GetLightAttenuation(worldPosition, i, lightDir);

            lightColor *= MiePhase(dot(lightDir, direction), mieG);  

            lightCol += lightColor;
        }
    #endif

	return lightCol;    
}