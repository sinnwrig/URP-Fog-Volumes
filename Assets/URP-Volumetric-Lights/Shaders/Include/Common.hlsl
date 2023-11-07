#pragma once

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define LINEAR_EYE_DEPTH(depth) LinearEyeDepth(depth, _ZBufferParams)
#define LINEAR_01_DEPTH(depth) Linear01Depth(depth, _ZBufferParams)

#define SAMPLE_BASE(_Tex, sampler_Tex, uv) SAMPLE_TEXTURE2D_LOD(_Tex, sampler_Tex, uv, 0)
#define SAMPLE_BASE3D(_Tex, sampler_Tex, uv) SAMPLE_TEXTURE3D_LOD(_Tex, sampler_Tex, uv, 0)