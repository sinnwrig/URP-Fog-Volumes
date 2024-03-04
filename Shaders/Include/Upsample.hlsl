// Original project by Michal Skalsky under the BSD license : https://github.com/SlightlyMad/VolumetricLights/blob/master/Assets/Shaders/BilateralBlur.shader
// Modified by Kai Angulo

#pragma once


#define UPSAMPLE_DEPTH_THRESHOLD 1.5f


TEXTURE2D_X(_CameraDepthTexture);

TEXTURE2D_X(_DownsampleDepth); 
SAMPLER(sampler_DownsampleDepth);
float4 _DownsampleDepth_TexelSize;

TEXTURE2D(_DownsampleColor);
SAMPLER(sampler_DownsampleColor);


struct v2fUpsample
{
	float2 uv : TEXCOORD0;
	float2 uv00 : TEXCOORD1;
	float2 uv01 : TEXCOORD2;
	float2 uv10 : TEXCOORD3;
	float2 uv11 : TEXCOORD4;
	float4 vertex : SV_POSITION;
};



v2fUpsample VertUpsample(appdata v)
{
	float2 texelSize = _DownsampleDepth_TexelSize;

    v2fUpsample o;
    o.vertex = CorrectUV(v.vertex);
    o.uv = v.uv;

    o.uv00 = v.uv - 0.5 * texelSize.xy;
    o.uv10 = o.uv00 + float2(texelSize.x, 0);
    o.uv01 = o.uv00 + float2(0, texelSize.y);
    o.uv11 = o.uv00 + texelSize.xy;

    return o;
}


half4 DepthAwareUpsample(v2fUpsample input) : SV_TARGET
{
    const float threshold = UPSAMPLE_DEPTH_THRESHOLD;

    float4 highResDepth = LINEAR_EYE_DEPTH(SAMPLE_BASE(_CameraDepthTexture, sampler_DownsampleDepth, input.uv).x).xxxx;
	float4 lowResDepth;

    lowResDepth.x = LINEAR_EYE_DEPTH(SAMPLE_BASE(_DownsampleDepth, sampler_DownsampleDepth, input.uv00).x);
    lowResDepth.y = LINEAR_EYE_DEPTH(SAMPLE_BASE(_DownsampleDepth, sampler_DownsampleDepth, input.uv10).x);
    lowResDepth.z = LINEAR_EYE_DEPTH(SAMPLE_BASE(_DownsampleDepth, sampler_DownsampleDepth, input.uv01).x);
    lowResDepth.w = LINEAR_EYE_DEPTH(SAMPLE_BASE(_DownsampleDepth, sampler_DownsampleDepth, input.uv11).x);

	float4 depthDiff = abs(lowResDepth - highResDepth);
	float accumDiff = dot(depthDiff, float4(1, 1, 1, 1));

	if (accumDiff < threshold) // Small error, not an edge -> use bilinear filter
		return SAMPLE_BASE(_DownsampleColor, sampler_DownsampleColor, input.uv);
    
	// Find nearest sample
	float minDepthDiff = depthDiff.x;
	float2 nearestUv = input.uv00;

	if (depthDiff.y < minDepthDiff)
	{
		nearestUv = input.uv10;
		minDepthDiff = depthDiff.y;
	}

	if (depthDiff.z < minDepthDiff)
	{
		nearestUv = input.uv01;
		minDepthDiff = depthDiff.z;
	}

	if (depthDiff.w < minDepthDiff)
	{
		nearestUv = input.uv11;
		minDepthDiff = depthDiff.w;
	}

    return SAMPLE_BASE(_DownsampleColor, sampler_DownsampleDepth, nearestUv);
}