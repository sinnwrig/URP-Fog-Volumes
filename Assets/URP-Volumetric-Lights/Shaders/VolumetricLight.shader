// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo


Shader "Hidden/VolumetricLight"
{	

	HLSLINCLUDE

	#include "/Include/Common.hlsl"

	struct appdata
	{
		float4 vertex : POSITION;
		float2 uv : TEXCOORD0;
	};


	struct v2f
	{
		float4 vertex : SV_POSITION;
		float2 uv : TEXCOORD0;
		float3 viewVector : TEXCOORD2;
	};


	ENDHLSL


	SubShader
	{
		// Pass 0 - Spot Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always
			Blend One One

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0

			#pragma multi_compile_fragment _ SPOT_LIGHT
			#pragma multi_compile_fragment _ POINT_LIGHT
			#pragma multi_compile_fragment _ DIRECTIONAL_LIGHT
			#pragma multi_compile_fragment _ NOISE
			
			#include "/Include/Math.hlsl"
			#include "/Include/Intersection.hlsl"
			#include "Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"	


			TEXTURE2D(_SourceTexture);
			SAMPLER(sampler_SourceTexture);
			
			TEXTURE2D_X(_CameraDepthTexture);
			SAMPLER(sampler_CameraDepthTexture);
			

			float4 _ViewportRect;


			void ClipViewport(inout float4 clipPos, inout float2 uv)
			{
			#if UNITY_UV_STARTS_AT_TOP
				clipPos.y *= -1;
			#endif

				float2 clip01 = clipPos.xy * 0.5 + 0.5;

				clip01 = min(max(clip01, _ViewportRect.xy), _ViewportRect.xy + _ViewportRect.zw);
				uv = clip01;

				clipPos.xy = clip01 * 2 - 1;

			#if UNITY_UV_STARTS_AT_TOP
				clipPos.y *= -1;
			#endif
			}


			v2f vert(appdata v)
			{
				v2f output = (v2f)0;
				output.vertex = TransformObjectToHClip(v.vertex.xyz);
				output.uv = v.uv;

				//ClipViewport(output.vertex, output.uv);

			    // Get view vector using UV
				float3 viewVector = mul(unity_CameraInvProjection, float4(output.uv * 2 - 1, 0, -1)).xyz;

			    // Transform to world space
				output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

				return output;
			}


			half4 frag(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;

				float len = length(i.viewVector);
				float3 rayDir = i.viewVector / len;				

				half4 scene = SAMPLE_TEXTURE2D(_SourceTexture, sampler_SourceTexture, uv);
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_EYE_DEPTH(depth) * len;

				return CalculateVolumetricLight(scene, uv, _WorldSpaceCameraPos.xyz, rayDir, linearDepth);
			}

			ENDHLSL
		}

		// Pass 1 - Blit add into result
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
