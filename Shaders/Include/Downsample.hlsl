// Original project by Michal Skalsky under the BSD license : https://github.com/SlightlyMad/VolumetricLights/blob/master/Assets/Shaders/BilateralBlur.shader
// Modified by Kai Angulo

#pragma once


// method used to downsample depth buffer: 0 = min; 1 = max; 2 = min/max in chessboard pattern
#define DOWNSAMPLE_DEPTH_MODE 2


TEXTURE2D_X(_DownsampleSource);     
SAMPLER(sampler_DownsampleSource);
float4 _DownsampleSource_TexelSize;


struct v2fDownsample
{
	float2 uv : TEXCOORD0;
	float2 uv00 : TEXCOORD1;
	float2 uv01 : TEXCOORD2;
	float2 uv10 : TEXCOORD3;
	float2 uv11 : TEXCOORD4;
	float4 vertex : SV_POSITION;
};



v2fDownsample DownsampleVertex(appdata v)
{
	float2 texelSize = _DownsampleSource_TexelSize;

	v2fDownsample o;
	o.vertex = CorrectUV(v.vertex);
	o.uv = v.uv;
	
	o.uv00 = v.uv - 0.5 * texelSize.xy; // Offset bottom-left
	o.uv10 = o.uv00 + float2(texelSize.x, 0); // Offset bottom right
	o.uv01 = o.uv00 + float2(0, texelSize.y); // Offset top left
	o.uv11 = o.uv00 + texelSize.xy; // Offset top right
    
	return o;
}



float DownsampleFragment(v2fDownsample input) : SV_TARGET
{
	float4 depth;

	// Sample pixels in corners
	depth.x = SAMPLE_BASE(_DownsampleSource, sampler_DownsampleSource, input.uv00).x;
	depth.y = SAMPLE_BASE(_DownsampleSource, sampler_DownsampleSource, input.uv01).x;
	depth.z = SAMPLE_BASE(_DownsampleSource, sampler_DownsampleSource, input.uv10).x;
	depth.w = SAMPLE_BASE(_DownsampleSource, sampler_DownsampleSource, input.uv11).x;

#if DOWNSAMPLE_DEPTH_MODE == 0 // min depth
    return min(min(depth.x, depth.y), min(depth.z, depth.w));

#elif DOWNSAMPLE_DEPTH_MODE == 1 // max depth
    return max(max(depth.x, depth.y), max(depth.z, depth.w));

#elif DOWNSAMPLE_DEPTH_MODE == 2 // min/max depth in chessboard pattern

	float minDepth = min(min(depth.x, depth.y), min(depth.z, depth.w));
	float maxDepth = max(max(depth.x, depth.y), max(depth.z, depth.w));

    // chessboard pattern
    int2 position = input.vertex.xy % 2;
    int index = position.x + position.y;
    return index == 1 ? minDepth : maxDepth;
#endif

}