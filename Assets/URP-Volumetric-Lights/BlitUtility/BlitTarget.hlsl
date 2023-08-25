#ifndef BLIT_TARGET_INCLUDED
#define BLIT_TARGET_INCLUDED

TEXTURE2D(_BlitTarget);
SAMPLER(sampler_BlitTarget);


float4 SampleBlitColor(float2 uv) 
{
    return SAMPLE_TEXTURE2D(_BlitTarget, sampler_BlitTarget, uv);
}

#endif