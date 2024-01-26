#pragma once

uint2 _TileSize; // Size of each reprojected tile
uint2 _PassOffset; // Where on the reprojection 'tile' are we rendering right now?
uint2 _ReprojectionRenderSize; // Size of the low-res texture that will be reprojected


float4x4 _PrevView;
float4x4 _PrevViewProjection;
float4x4 _PrevInverseViewProjection;

float4x4 _CameraView;
float4x4 _CameraViewProjection;
float4x4 _InverseViewProjection;


bool SkipReprojectPixel(float2 uv)
{
    uint2 pixelPos = uv * _ScreenParams.xy;
    uint2 tilePos = pixelPos % _TileSize;

    return tilePos != _PassOffset;
}


float2 FullUVFromLowResUV(float2 lowresUV)
{
    uint2 pixelPos = lowresUV * _ReprojectionRenderSize * _TileSize;
    uint2 tile = pixelPos + _PassOffset;

    return (float2)tile / _ScreenParams.xy;
}


// URP's MotionVectors don't seem to work reliably, so get the camera motion vectors ourselves
// Modified from https://github.com/Kink3d/kMotion/blob/master/Shaders/CameraMotionVectors.shader
float2 GetVelocity(float2 uv, TEXTURE2D_PARAM(_DepthTexture, sampler_DepthTexture))
{
    // Calculate PositionInputs
    float depth = SAMPLE_DEPTH_TEXTURE(_DepthTexture, sampler_DepthTexture, uv).x;

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



half4 ReprojectPixel(float2 uv, TEXTURE2D_PARAM(_SourceColor, sampler_SourceColor), TEXTURE2D_PARAM(_LowresSample, sampler_LowresSample), TEXTURE2D_PARAM(_DepthTexture, sampler_DepthTexture))
{
    half2 motion = GetVelocity(uv, TEXTURE2D_ARGS(_DepthTexture, sampler_DepthTexture));

    float2 sampleUv = uv - motion;

    if (!SkipReprojectPixel(uv))
        return SAMPLE_BASE(_LowresSample, sampler_LowresSample, uv);
    
    // Reproject this pixel if possible
    if (sampleUv.x >= 0 && sampleUv.x <= 1 && sampleUv.y >= 0 && sampleUv.y <= 1)
        return SAMPLE_BASE(_SourceColor, sampler_SourceColor, sampleUv);

    return SAMPLE_BASE(_SourceColor, sampler_SourceColor, uv);
}


