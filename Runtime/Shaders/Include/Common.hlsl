#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Depth sampler macros
#define LINEAR_EYE_DEPTH(depth) LinearEyeDepth(depth, _ZBufferParams)
#define LINEAR_01_DEPTH(depth) Linear01Depth(depth, _ZBufferParams)


// LOD 0 sampler macros to prevent use of gradient functions
#define SAMPLE_BASE(_Tex, sampler_Tex, uv) SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv, 0)
#define SAMPLE_BASE3D(_Tex, sampler_Tex, uv) SAMPLE_TEXTURE3D_LOD(_Tex, sampler_Tex, uv, 0)


inline float4 CorrectVertex(float4 vertex)
{
    #if UNITY_UV_STARTS_AT_TOP
        vertex.y *= -1;
    #endif

    return vertex;
}