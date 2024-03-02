// Original project by Michal Skalsky under the BSD license : https://github.com/SlightlyMad/VolumetricLights/blob/master/Assets/Shaders/BilateralBlur.shader
// Modified by Kai Angulo

#pragma once

     
#define GAUSS_BLUR_DEVIATION 2.5

#if defined(FULL_RES_BLUR)
	#define KERNEL_SIZE 7
#elif defined(HALF_RES_BLUR)
	#define KERNEL_SIZE 5
#elif defined(QUARTER_RES_BLUR)
	#define KERNEL_SIZE 6
#else
	#define KERNEL_SIZE 0
#endif


const float GaussianWeight(float offset, float deviation)
{
	float weight = 1.0f / sqrt(2.0f * PI * sqr(deviation));
	weight *= exp(-(sqr(offset)) / (2.0f * sqr(deviation)));
	return weight;
}


half4 BilateralBlur(float2 uv, TEXTURE2D_PARAM(_SourceBlurTex, sampler_SourceBlurTex), float4 _TexelSize, const int2 direction)
{
	const float deviation = KERNEL_SIZE / GAUSS_BLUR_DEVIATION; 

	half4 centerColor = SAMPLE_BASE(_SourceBlurTex, sampler_SourceBlurTex, uv);

	float weight = 0;
	float weightSum = 0;

	float3 color = 0;

	// Pixels from left/down to right/up of center pixel
	[unroll] 
	for (int i = -KERNEL_SIZE; i <= KERNEL_SIZE; i++)
	{
        float2 offset = (direction * i) * _TexelSize.xy;

    	half3 sampleColor = SAMPLE_BASE(_SourceBlurTex, sampler_SourceBlurTex, uv + offset);

	    // gaussian weight is computed from constants only -> will be computed in compile time
	    weight = GaussianWeight(i, deviation);

		color += weight * sampleColor;
		weightSum += weight;
	}

	color /= weightSum;
	return half4(color, centerColor.w);
}