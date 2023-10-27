#if defined(USING_STEREO_MATRICES) && defined(UNITY_STEREO_INSTANCING_ENABLED)
    #define GET_INVERSE_PROJECTION _CorgiInverseProjectionArray[unity_StereoEyeIndex]
    #define SAMPLE_TEXTURE2D_X_BIAS(textureName, samplerName, coord2, bias)                  textureName.Sample(samplerName, float3(coord2, unity_StereoEyeIndex), bias)
#else
    #define GET_INVERSE_PROJECTION _CorgiInverseProjection
    #define SAMPLE_TEXTURE2D_X_BIAS(textureName, samplerName, coord2, bias)                  textureName.Sample(samplerName, coord2, bias)
#endif

#if UNITY_VERSION >= 202120
    // if unity version 2021.2.0 or higher, then we'll be using URP 12 or higher, which has light macros defined
    // so just use those macros directly, to support future versions of URP. unless they break that too ?? 
    #define CORGI_LIGHT_LOOP_BEGIN LIGHT_LOOP_BEGIN
    #define CORGI_LIGHT_LOOP_END LIGHT_LOOP_END
#else
    // redefining some URP light macros here, to support older URP versions 
    // note: taken from URP 12's RealtimeLights.hlsl 
    #if !defined(_USE_WEBGL1_LIGHTS) && defined(UNITY_PLATFORM_WEBGL) && !defined(SHADER_API_GLES3)
        #define CORGI_USE_WEBGL1_LIGHTS 1
        #define CORGI_WEBGL1_MAX_LIGHTS 8
    #else
        #define CORGI_USE_WEBGL1_LIGHTS 0
    #endif

    #if !CORGI_USE_WEBGL1_LIGHTS
        #define CORGI_LIGHT_LOOP_BEGIN(lightCount) \
        for (uint lightIndex = 0u; lightIndex < lightCount; ++lightIndex) {

        #define CORGI_LIGHT_LOOP_END }
    #else
        #define CORGI_LIGHT_LOOP_BEGIN(lightCount) \
        for (int lightIndex = 0; lightIndex < _WEBGL1_MAX_LIGHTS; ++lightIndex) { \
            if (lightIndex >= (int)lightCount) break;

        #define CORGI_LIGHT_LOOP_END }
    #endif
#endif

float4x4 _CorgiInverseProjectionArray[2];
float4x4 _CorgiInverseProjection;
float4x4 _CorgiCameraToWorld;
float3 _CorgiMainLightDirection;
float _CorgiVisibleLightCount;

TEXTURE2D_X(_CorgiDepthGrabpassFullRes);
SAMPLER(sampler_CorgiDepthGrabpassFullRes);


float3 GetWorldSpacePosition(float2 uv)
{
    // todo: z direction 
    float depth = SAMPLE_TEXTURE2D_X(_CorgiDepthGrabpassFullRes, sampler_CorgiDepthGrabpassFullRes, uv).r;

    float4 clip = float4(2.0 * uv - 1.0, depth, 1.0);
    float4 viewPos = mul(GET_INVERSE_PROJECTION, clip);
    viewPos.xyz /= viewPos.w;
    viewPos.w = 1;

    float3 worldPos = mul(_CorgiCameraToWorld, viewPos).xyz;
    return worldPos;
}

// https://github.com/mrdooz/kumi/blob/master/effects/luminance.hlsl
float CalcLuminance(float3 color)
{
    return dot(color, float3(0.299f, 0.587f, 0.114f));
}


StructuredBuffer<LightData> _CorgiVisibleLightData;
StructuredBuffer<int> _CorgiLightIndexToShadowIndex;


float GetCorgiLightCount()
{
    return _CorgiVisibleLightCount;
}

// source: unity URP's lighting includes 
Light GetCorgiAdditionalPerObjectLight(int perObjectLightIndex, float3 positionWS)
{
    float4 lightPositionWS = _CorgiVisibleLightData[perObjectLightIndex].position;
    half3 color = _CorgiVisibleLightData[perObjectLightIndex].color.rgb;
    half4 distanceAndSpotAttenuation = _CorgiVisibleLightData[perObjectLightIndex].attenuation;
    half4 spotDirection = _CorgiVisibleLightData[perObjectLightIndex].spotDirection;

    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float3 lightVector = lightPositionWS.xyz - positionWS * lightPositionWS.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
    half attenuation = half(DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy) * AngleAttenuation(spotDirection.xyz, lightDirection, distanceAndSpotAttenuation.zw));

    Light light;
    light.direction = lightDirection;
    light.distanceAttenuation = attenuation;
    light.shadowAttenuation = 1.0; // This value can later be overridden in GetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)
    light.color = color;

    // layermasks were supported in 2021.x.x+ (URP 12+)
#if UNITY_VERSION >= 2021000
    light.layerMask = 0;
#endif

    return light;
}

int CorgiLightToShadowIndex(int lightIndex)
{
    return _CorgiLightIndexToShadowIndex[lightIndex];
}