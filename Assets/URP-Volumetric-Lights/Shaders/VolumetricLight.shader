// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo


Shader "Hidden/VolumetricLight"
{	
	SubShader
	{
		// Pass 0 - Spot Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex VolumetricVertex
			#pragma fragment VolumetricFragment
			#pragma target 4.0

			#pragma multi_compile NOISE

			#define SPOT_LIGHT

			#include "/Include/Common.hlsl"
			#include "/Include/Math.hlsl"
			#include "/Include/Intersection.hlsl"
			#include "/Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"

			ENDHLSL
		}

		// Pass 1 - Point Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex VolumetricVertex
			#pragma fragment VolumetricFragment
			#pragma target 4.0

			#pragma multi_compile NOISE

			#define POINT_LIGHT

			#include "/Include/Common.hlsl"
			#include "/Include/Math.hlsl"
			#include "/Include/Intersection.hlsl"
			#include "/Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"

			ENDHLSL
		}

		// Pass 2 - Directional Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex VolumetricVertex
			#pragma fragment VolumetricFragment
			#pragma target 4.0

			#pragma multi_compile NOISE

			#define DIRECTIONAL_LIGHT

			#include "/Include/Common.hlsl"
			#include "/Include/Math.hlsl"
			#include "/Include/Intersection.hlsl"
			#include "/Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"

			ENDHLSL
		}

		// Pass 3 - Blit add into result
		Pass
		{
			Cull Off ZWrite Off ZTest Always

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
	}
}
