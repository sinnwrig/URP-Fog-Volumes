Shader "Hidden/VolumetricFog"
{	
	SubShader
	{
		Pass
		{
			Cull Off ZWrite Off ZTest Off
			Blend One One

			HLSLPROGRAM

			#pragma vertex VolumetricVertex
			#pragma fragment VolumetricFragment
			#pragma target 4.0

			// URP keywords
			#pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS 
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _LIGHT_COOKIES

			// Custom keywords
			#pragma multi_compile_fragment _ NOISE_ENABLED
			#pragma multi_compile_fragment _ TEMPORAL_RENDERING_ENABLED
   			#pragma multi_compile_fragment _ LIGHTING_ENABLED SHADOWS_ENABLED 

			#pragma multi_compile_fragment _ SPHERE_VOLUME CUBE_VOLUME CAPSULE_VOLUME CYLINDER_VOLUME

			#define MAX_LIGHT_COUNT 32

			#include "/Include/Common.hlsl"
			#include "/Include/Math.hlsl"
			#include "/Include/Intersection.hlsl"
			#include "/Include/LightAttenuation.hlsl"
			#include "/Include/Reprojection.hlsl"
			#include "/Include/VolumetricFog.hlsl"

			ENDHLSL
		}
	}
}
