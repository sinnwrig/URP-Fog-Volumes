#pragma once


//--------------------------------------------------------------------------------------------
// Downsample, bilateral blur and upsample config
//--------------------------------------------------------------------------------------------        
#define GAUSS_BLUR_DEVIATION 1.5
#define BLUR_DEPTH_FACTOR 0.5 
//--------------------------------------------------------------------------------------------


TEXTURE2D(_BlurSource);
SAMPLER(sampler_BlurSource);


TEXTURE2D(_DepthTexture);     
SAMPLER(sampler_DepthTexture);
float4 _DepthTexture_TexelSize;



float GaussianWeight(float offset, float deviation)
{
	float weight = 1.0f / sqrt(2.0f * PI * deviation * deviation);
	weight *= exp(-(offset * offset) / (2.0f * deviation * deviation));
	return weight;
}



float4 BilateralBlur(v2f input, int2 direction, const int kernelRadius)
{
	//const float deviation = kernelRadius / 2.5;
	const float deviation = kernelRadius / GAUSS_BLUR_DEVIATION; // make it really strong
	
    float2 uv = input.uv;
	float4 centerColor = SAMPLE_TEXTURE2D(_BlurSource, sampler_BlurSource, uv);
	float3 color = centerColor.xyz;

	float centerDepth = (LINEAR_EYE_DEPTH(SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, uv)));

	float weightSum = 0;

	// gaussian weight is computed from constants only -> will be computed in compile time
    float weight = GaussianWeight(0, deviation);
	color *= weight;
	weightSum += weight;

	// Pixels to left/down of center pixel		
	[unroll] 
	for (int i = -kernelRadius; i < 0; i++)
	{
        float2 offset = (direction * i) * _DepthTexture_TexelSize.xy;

        float3 sampleColor = SAMPLE_TEXTURE2D(_BlurSource, sampler_BlurSource, uv + offset);
        float sampleDepth = (LINEAR_EYE_DEPTH(SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, input.uv + offset)));

		float depthDiff = abs(centerDepth - sampleDepth);
        float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
		float w = exp(-(dFactor * dFactor));

	    // gaussian weight is computed from constants only -> will be computed in compile time
	    weight = GaussianWeight(i, deviation) * w;

		color += weight * sampleColor;
		weightSum += weight;
	}

	
	// Pixels to right/up of center pixel
    [unroll] 
    for (i = 1; i <= kernelRadius; i++)
    {
    	float2 offset = (direction * i) * _DepthTexture_TexelSize.xy;

        float3 sampleColor = SAMPLE_TEXTURE2D(_BlurSource, sampler_BlurSource, input.uv + offset);
        float sampleDepth = (LINEAR_EYE_DEPTH(SAMPLE_TEXTURE2D(_DepthTexture, sampler_DepthTexture, input.uv + offset)));

		float depthDiff = abs(centerDepth - sampleDepth);
        float dFactor = depthDiff * BLUR_DEPTH_FACTOR;
		float w = exp(-(dFactor * dFactor));
				
	    // gaussian weight is computed from constants only -> will be computed in compile time
	    weight = GaussianWeight(i, deviation) * w;

		color += weight * sampleColor;
		weightSum += weight;
	}

	color /= weightSum;
	return float4(color, centerColor.w);
}