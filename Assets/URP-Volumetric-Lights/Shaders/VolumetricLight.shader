// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo


Shader "Hidden/VolumetricLight"
{	

	HLSLINCLUDE

	#include "/Include/Common.hlsl"
	#include "/Include/Math.hlsl"
	#include "/Include/Intersection.hlsl"
	#include "Include/LightAttenuation.hlsl"

	#pragma multi_compile NOISE

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


	v2f vertObj(appdata v)
	{
		v2f output = (v2f)0;

		output.worldPos = TransformObjectToWorld(v.vertex.xyz);
		output.vertex = TransformWorldToHClip(output.worldPos);
		
		output.uv = output.vertex.xy / output.vertex.w * 0.5 + 0.5;

	#if UNITY_UV_STARTS_AT_TOP
		output.uv.y = 1 - output.uv.y;
	#endif

		return output;
	}


	ENDHLSL


	SubShader
	{
		// Pass 0 - Spot Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex vertObj
			#pragma fragment frag
			#pragma target 4.0

			
			#define SPOT_LIGHT
			#include "Include/VolumetricLight.hlsl"

			
			TEXTURE2D_X(_CameraDepthTexture);
			SAMPLER(sampler_CameraDepthTexture);


			half4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;

				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.worldPos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir);

				rayDir /= rayLength;

				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_EYE_DEPTH(depth) * rayLength;

				return CalculateVolumetricLight(uv, UNITY_MATRIX_I_M, rayStart, rayDir, linearDepth);
			}

			ENDHLSL
		}

		// Pass 1 - Point Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex vertObj
			#pragma fragment frag
			#pragma target 4.0

			#define POINT_LIGHT
			#include "Include/VolumetricLight.hlsl"

			
			TEXTURE2D_X(_CameraDepthTexture);
			SAMPLER(sampler_CameraDepthTexture);


			half4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;

				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.worldPos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir);

				rayDir /= rayLength;

				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_EYE_DEPTH(depth) * rayLength;

				return CalculateVolumetricLight(uv, UNITY_MATRIX_I_M, rayStart, rayDir, linearDepth);
			}

			ENDHLSL
		}

		// Pass 2 - Directional Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0

			#define DIRECTIONAL_LIGHT
			#include "Include/VolumetricLight.hlsl"

			
			TEXTURE2D_X(_CameraDepthTexture);
			SAMPLER(sampler_CameraDepthTexture);


			v2f vert(appdata v)
			{
				v2f output = (v2f)0;
				output.vertex = v.vertex;
				output.uv = v.uv;

			#if UNITY_UV_STARTS_AT_TOP
				output.uv.y = 1 - output.uv.y;
			#endif

			    // Get view vector using UV
				float3 viewVector = mul(unity_CameraInvProjection, float4(output.uv * 2 - 1, 0, -1)).xyz;
			    // Transform to world space
				output.worldPos = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

				return output;
			}


			half4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;

				float len = length(i.worldPos);
				float3 rayDir = i.worldPos / len;				

				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_EYE_DEPTH(depth) * len;

				return CalculateVolumetricLight(uv, UNITY_MATRIX_I_M, _WorldSpaceCameraPos.xyz, rayDir, linearDepth);
			}

			ENDHLSL
		}

		// Pass 3 - Blit add into result
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			TEXTURE2D(_BlitSource);
			SAMPLER(sampler_BlitSource);

			TEXTURE2D(_BlitAdd);
			SAMPLER(sampler_BlitAdd);


			v2f vert(appdata v)
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
