#pragma once

int _TemporalPassCount;
int _TemporalPass;

float4x4 _PrevView;
float4x4 _PrevViewProjection;
float4x4 _PrevInverseViewProjection;

float4x4 _CameraView;
float4x4 _CameraViewProjection;
float4x4 _InverseViewProjection;


bool SkipReprojectPixel(float2 uv)
{
    int2 pixCoords = int2(uv.x * _ScreenParams.x, uv.y * _ScreenParams.y);
    return pixCoords.x % _TemporalPassCount != _TemporalPass && pixCoords.y % _TemporalPassCount != _TemporalPass;
}


// URP's MotionVectors don't seem to work reliably, so get the camera motion vectors ourselves
// Modified from https://github.com/Kink3d/kMotion/blob/master/Shaders/CameraMotionVectors.shader
float2 GetVelocity(float2 uv)
{
    // Calculate PositionInputs
    float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv).x;

    half2 screenSize = half2(1 / _ScreenParams.x, 1 / _ScreenParams.y);

    PositionInputs prevInputs = GetPositionInput(uv, screenSize, depth, _PrevInverseViewProjection, _PrevView);
    PositionInputs positionInputs = GetPositionInput(uv, screenSize, depth, _InverseViewProjection, _CameraView);

    // Calculate clip-space positions
    float4 previousPositionVP = mul(_PrevViewProjection, float4(prevInputs.positionWS, 1.0));
    previousPositionVP.xy = previousPositionVP.xy / previousPositionVP.w;

    float4 positionVP = mul(_CameraViewProjection, float4(positionInputs.positionWS, 1.0));
    positionVP.xy = positionVP.xy / positionVP.w;

    return (positionVP.xy - previousPositionVP.xy) * 0.5;
}



half4 ReprojectPixel(float2 uv, TEXTURE2D_PARAM(_SourceColor, sampler_SourceColor))
{
    half2 motion = GetVelocity(uv);

    float2 sampleUv = uv - motion;

    if (!SkipReprojectPixel(uv))
        return 0.0;

    // Reproject this pixel if possible
    if (sampleUv.x >= 0 && sampleUv.x <= 1 && sampleUv.y >= 0 && sampleUv.y <= 1)
        return SAMPLE_BASE(_SourceColor, sampler_SourceColor, sampleUv);

    return SAMPLE_BASE(_SourceColor, sampler_SourceColor, uv);
}


