#ifndef LIGHT_ATTEN_INCLUDED
#define LIGHT_ATTEN_INCLUDED

// URP keywords
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ _LIGHT_COOKIES

// Unity keywords
#pragma multi_compile _ _CLUSTERED_RENDERING
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"



half3 GetLightAttenuation(Light light)
{
    return light.color * light.distanceAttenuation * light.shadowAttenuation;
}


half3 GetMainLightContribution(float3 worldPosition)
{    
    float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
    
    Light light = GetMainLight();
    light.shadowAttenuation = MainLightRealtimeShadow(shadowCoord);

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleMainLightCookie(worldPosition);
        light.color *= cookieColor;
    #endif

    return GetLightAttenuation(light);
}


half3 GetAdditionalLightContribution(uint lightIndex, float3 worldPosition)
{
    #if !defined(USE_FORWARD_PLUS)
        lightIndex = GetPerObjectLightIndex(lightIndex);
    #endif

    Light light = GetAdditionalPerObjectLight(lightIndex, worldPosition);

    light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, worldPosition, light.direction);

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleAdditionalLightCookie(lightIndex, worldPosition);
        light.color *= cookieColor;
    #endif

    return GetLightAttenuation(light);
}

#endif