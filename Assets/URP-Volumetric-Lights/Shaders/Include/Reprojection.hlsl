#pragma once

TEXTURE2D_X(_MotionVectorTexture);
SAMPLER(sampler_MotionVectorTexture);


int _PassIndex;
int _TemporalPasses;


bool SkipReprojectPixel(float2 uv)
{
#if defined(TEMPORAL_REPROJECTION_ENABLED)
    float2 fPixelPos = uv.xy * _ScreenParams.xy;
    int2 pixelPos = int2(fPixelPos); 

    return pixelPos.x % _TemporalPasses != _PassIndex && pixelPos.y % _TemporalPasses != _PassIndex;
#endif
    return false;
}



half3 ReprojectPixel(float2 uv, TEXTURE2D_PARAM(_SourceColor, sampler_SourceColor))
{
    float2 motion = SAMPLE_BASE(_MotionVectorTexture, sampler_MotionVectorTexture, uv);

    float2 sampleUv = uv - motion;

    if (sampleUv.x >= 0 || sampleUv.x <= 1 || sampleUv.y >= 0 || sampleUv.y <= 1 || !SkipReprojectPixel(uv))
        return SAMPLE_BASE(_SourceColor, sampler_SourceColor, sampleUv).xyz;
    
    return 0.0;
}