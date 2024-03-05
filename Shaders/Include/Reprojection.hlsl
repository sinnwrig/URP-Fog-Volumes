#pragma once

uint2 _TileSize;
uint2 _PassOffset; 
uint2 _TemporalRenderSize;
float _MotionInfluence;


bool SkipReprojectPixel(float2 uv, out int2 lowresPixel)
{
    uint2 pixelPos = uv * _ScreenParams.xy;

    lowresPixel = pixelPos / _TileSize;
    uint2 tileOffset = pixelPos % _TileSize;

    return tileOffset.x != _PassOffset.x || tileOffset.y != _PassOffset.y;
}


float2 FullUVFromLowResUV(float2 lowresUV)
{
    uint2 pixelPos = (lowresUV * _TemporalRenderSize);
    uint2 tile = pixelPos * _TileSize + _PassOffset;

    return (float2)tile / _ScreenParams.xy;
}


half4 ReprojectPixel(float2 uv, TEXTURE2D_PARAM(_SourceColor, sampler_SourceColor), TEXTURE2D_PARAM(_LowresSample, sampler_LowresSample), TEXTURE2D_PARAM(_MotionVectors, sampler_MotionVectors))
{
    float2 motion = SAMPLE_TEXTURE2D_X(_MotionVectors, sampler_MotionVectors, uv);
    float2 sampleUv = uv - (motion * _MotionInfluence);

    int2 lowresPixel;
    if (!SkipReprojectPixel(uv, lowresPixel))
        return LOAD_TEXTURE2D(_LowresSample, lowresPixel);

    const float motionThreshold;
    
    // Reproject this pixel if possible
    if (sampleUv.x >= 0 && sampleUv.x <= 1 && 
        sampleUv.y >= 0 && sampleUv.y <= 1 && 
        abs(sampleUv.x - uv.x) < motionThreshold && 
        abs(sampleUv.y - uv.y) < motionThreshold)
    {
        return SAMPLE_BASE(_SourceColor, sampler_SourceColor, sampleUv);
    }

    return SAMPLE_BASE(_SourceColor, sampler_SourceColor, uv);
}


