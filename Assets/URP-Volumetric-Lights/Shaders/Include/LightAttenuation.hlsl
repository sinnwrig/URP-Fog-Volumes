#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/BRDF.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"


int _LightIndex;

float4 _LightPosition;
half3 _LightColor;
half4 _LightAttenuation;
half4 _SpotDirection;



half3 GetMainLightColor(float3 worldPosition)
{    
    float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
    half3 color = _LightColor * MainLightRealtimeShadow(shadowCoord);

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleMainLightCookie(worldPosition);
        color *= cookieColor;
    #endif

    return color;
}


half3 GetAdditionalLightColor(float3 worldPosition)
{
    float3 lightVector = _LightPosition.xyz - worldPosition * _LightPosition.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    float rsqr = rsqrt(distanceSqr);

    half3 lightDirection = half3(lightVector * rsqr);
    half distanceAttenuation = DistanceAttenuation(distanceSqr, _LightAttenuation.xy) * AngleAttenuation(_SpotDirection.xyz, lightDirection, _LightAttenuation.zw);

    float shadowAttenuation = AdditionalLightRealtimeShadow(_LightIndex, worldPosition, lightDirection);

    half3 color = _LightColor;

    #if defined(_LIGHT_COOKIES)
        real3 cookieColor = SampleAdditionalLightCookie(_LightIndex, worldPosition);
        color *= cookieColor;
    #endif

    return color * shadowAttenuation * distanceAttenuation;
}



float4 GetLightAttenuation(float3 wpos)
{
	half3 lightCol = 1.0;

	if (_LightIndex < 0)
		lightCol = GetMainLightColor(wpos);
	else 
		lightCol = GetAdditionalLightColor(wpos);

	return float4(lightCol, 0.0);
}