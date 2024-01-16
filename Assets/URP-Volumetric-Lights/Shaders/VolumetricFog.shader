// Original project by Michal Skalsky under the BSD license 
// Modified by Kai Angulo


Shader "Hidden/VolumetricFog"
{	
	SubShader
	{
		// Pass 0 - Blit add into result
		Pass
		{
			Cull Off ZWrite Off ZTest Off

			HLSLPROGRAM

			#pragma vertex vertBlend
			#pragma fragment frag

			#include "/Include/Common.hlsl"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};


			struct v2f
			{
				float4 vertex : SV_POSITION;
				float3 worldPos : TEXCOORD0;
				float2 uv : TEXCOORD1;
			};

			TEXTURE2D(_BlitSource);
			SAMPLER(sampler_BlitSource);

			TEXTURE2D(_BlitAdd);
			SAMPLER(sampler_BlitAdd);


			v2f vertBlend(appdata v)
			{
				v2f output = (v2f)0;
				output.vertex = TransformObjectToHClip(v.vertex.xyz);
				output.uv = v.uv;

				return output;
			}


			half4 frag(v2f i) : SV_Target
			{
				float4 source = SAMPLE_TEXTURE2D(_BlitSource, sampler_BlitSource, i.uv);
				float4 sourceAdd = SAMPLE_TEXTURE2D(_BlitAdd, sampler_BlitAdd, i.uv);

				source.xyz += sourceAdd.xyz * min(sourceAdd.w, 1.0);

				return source;
			}

			ENDHLSL
		}

		// Pass 1 - Volumetric Fog
		Pass
		{
			Cull Off ZWrite Off ZTest Off
			Blend One One

			HLSLPROGRAM

			#pragma vertex VolumetricVertex
			#pragma fragment VolumetricFragment
			#pragma target 4.0

			// URP keywords
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _ADDITIONAL_LIGHTS
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_fragment _ _LIGHT_COOKIES

			// Custom keywords
			#pragma multi_compile _ NOISE_ENABLED
   			#pragma multi_compile _ LIGHTING_ENABLED
   			#pragma multi_compile _ SHADOWS_ENABLED

			#pragma multi_compile _ SPHERE_VOLUME
   			#pragma multi_compile _ CUBE_VOLUME
   			#pragma multi_compile _ CAPSULE_VOLUME
			#pragma multi_compile _ CYLINDER_VOLUME

			#define MAX_LIGHT_COUNT 64


			#include "/Include/Common.hlsl"
			#include "/Include/Math.hlsl"
			#include "/Include/Intersection.hlsl"
			#include "/Include/LightAttenuation.hlsl"
			#include "Include/VolumetricFog.hlsl"

			ENDHLSL
		}
	}
}
