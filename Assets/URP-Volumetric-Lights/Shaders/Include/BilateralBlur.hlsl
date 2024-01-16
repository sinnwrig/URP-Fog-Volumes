// Original project by Michal Skalsky under the BSD license 
// Modified by Kai Angulo

#pragma once

     
#define GAUSS_BLUR_DEVIATION 2.5


TEXTURE2D(_BlurSource);
SAMPLER(sampler_BlurSource);
float4 _BlurSource_TexelSize;


const float GaussianWeight(float offset, float deviation)
{
	float weight = 1.0f / sqrt(2.0f * PI * deviation * deviation);
	weight *= exp(-(offset * offset) / (2.0f * deviation * deviation));
	return weight;
}



float4 BilateralBlur(v2f input, int2 direction, const int kernelRadius)
{
	const float deviation = kernelRadius / GAUSS_BLUR_DEVIATION; 
	
    float2 uv = input.uv;

	float4 centerColor = SAMPLE_BASE(_BlurSource, sampler_BlurSource, uv);

	float weight = 0;
	float weightSum = 0;

	float3 color = 0;

	// Pixels from left/down to right/up of center pixel
	[unroll] 
	for (int i = -kernelRadius; i <= kernelRadius; i++)
	{
        float2 offset = (direction * i) * _BlurSource_TexelSize.xy;

        float3 sampleColor = SAMPLE_BASE(_BlurSource, sampler_BlurSource, uv + offset);

	    // gaussian weight is computed from constants only -> will be computed in compile time
	    weight = GaussianWeight(i, deviation);

		color += weight * sampleColor;
		weightSum += weight;
	}

	color /= weightSum;
	return float4(color, centerColor.w);
}