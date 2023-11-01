#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"


float4 lightPosition;
half3 lightColor;
half4 lightAttenuation;
half4 spotDirection;


// source: unity URP's lighting includes 
Light GetLight(float3 positionWS)
{
    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float3 lightVector = lightPosition.xyz - positionWS * lightPosition.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    half attenuation = half(DistanceAttenuation(distanceSqr, lightAttenuation.xy) * AngleAttenuation(spotDirection.xyz, lightDirection, lightAttenuation.zw));

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0;
    light.color = lightColor;

    return light;
}