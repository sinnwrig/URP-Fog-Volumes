#ifndef LIGHT_ATTEN_INCLUDED
#define LIGHT_ATTEN_INCLUDED


// URP keywords
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fragment _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ USE_FORWARD_PLUS

// Unity keywords
#pragma multi_compile _ _CLUSTERED_RENDERING
#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#pragma multi_compile _ SHADOWS_SHADOWMASK
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ DYNAMICLIGHTMAP_ON


#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

int _LightIndex;
int _ShadowIndex;
float4 _LightPosition;
half3 _LightColor;
half4 _LightAttenuation;
half4 _SpotDirection;


// source: unity URP's lighting includes 
Light GetLight(float3 positionWS)
{
    // Directional lights store direction in _LightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float3 lightVector = _LightPosition.xyz - positionWS * _LightPosition.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    half attenuation = half(DistanceAttenuation(distanceSqr, _LightAttenuation.xy) * AngleAttenuation(_SpotDirection.xyz, lightDirection, _LightAttenuation.zw));

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0;
    light.color = _LightColor;

    return light;
}



half3 GetMainLightContribution(float3 worldPosition)
{    
    float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
    half3 color = _LightColor * MainLightRealtimeShadow(shadowCoord);

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleMainLightCookie(lightIndex, worldPosition);
        color *= cookieColor;
    #endif

    return color;
}


half3 GetAdditionalLightContribution(uint lightIndex, float3 worldPosition)
{
    Light light = GetLight(worldPosition);

    light.shadowAttenuation = AdditionalLightRealtimeShadow(lightIndex, worldPosition, light.direction);

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleAdditionalLightCookie(_ShadowIndex, worldPosition);
        light.color *= cookieColor;
    #endif

    return light.color * light.shadowAttenuation * light.distanceAttenuation;
}



float4 GetLightAttenuation(float3 wpos)
{
	half3 lightCol = 1.0;

	if (_LightIndex < 0)
		lightCol = GetMainLightContribution(wpos);
	else 
		lightCol = GetAdditionalLightContribution(_LightIndex, wpos);

	return float4(lightCol, 0.0);
}

#endif