Shader "Hidden/VolumetricFog"
{	
	Properties
	{
		_NoiseTexture ("_NoiseTexture", 3D) = "white" {}
		_NoiseData ("_NoiseData", Vector) = (0, 0, 0, 0)
		_NoiseVelocity ("_NoiseVelocity", Vector) = (0, 0, 0, 0)

		_Albedo ("_Albedo", Color) = (0, 0, 0, 0)

		_Albedo ("_Ambient", Color) = (0, 0, 0, 0)
		_Intensity ("_Intensity", Float) = 1

        _StepParams ("_StepParams", Vector) = (0, 0, 0, 0)
        _SampleCount ("SampleCount", Int) = 0
    	_Jitter ("_Jitter", Float) = 0

		_IntensityModifier ("_IntensityModifier", Float) = 1
    	_MieG ("_MieG", Float) = 0
    	_Scattering ("_Scattering", Float) = 0
    	_Extinction ("_Extinction", Float) = 0

        _BrightnessClamp ("_BrightnessClamp", Float) = 0
	}

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
