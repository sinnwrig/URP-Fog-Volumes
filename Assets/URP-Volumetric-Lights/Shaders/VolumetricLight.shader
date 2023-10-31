// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo


Shader "Hidden/VolumetricLight"
{	

HLSLINCLUDE

#include "/Include/Common.hlsl"
#include "/Include/Math.hlsl"
#include "/Include/Intersection.hlsl"

#define MAX_STEPS 25
#define NOISE


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


v2f vert(appdata v)
{
	v2f output;
	output.vertex = TransformObjectToHClip(v.vertex.xyz);
	output.uv = v.uv;

    // Get view vector
	float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1)).xyz;

    // Transform to world space
	output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;
    
	return output;
}


TEXTURE2D(_SourceTexture);
SAMPLER(sampler_SourceTexture);

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);


ENDHLSL


	SubShader
	{
		// Pass 0 - Spot Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0

			#define SPOT_LIGHT
			
			#include "Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"	


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

		// Pass 1 - Point Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0

			#define POINT_LIGHT
			
			#include "Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"	


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

		// Pass 2 - Directional Light
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma target 4.0


			#define DIRECTIONAL_LIGHT

			#include "Include/LightAttenuation.hlsl"
			#include "Include/VolumetricLight.hlsl"	


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

		// Pass 3 - Blit add into result
		Pass
		{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			#pragma vertex addVert
			#pragma fragment frag

			TEXTURE2D(_SourceAdd);
			SAMPLER(sampler_SourceAdd);


			v2f addVert(appdata v)
			{
				v2f output;
				output.vertex = TransformObjectToHClip(v.vertex.xyz);
				output.uv = v.uv;
				output.viewVector = float3(0, 0, 0);

				return output;
			}


			half4 frag(v2f i) : SV_Target
			{
				float4 source = SAMPLE_TEXTURE2D(_SourceTexture, sampler_SourceTexture, i.uv);
				float4 sourceAdd = SAMPLE_TEXTURE2D(_SourceAdd, sampler_SourceAdd, i.uv);

				source.xyz += sourceAdd.xyz * min(sourceAdd.w, 1.0);

				return source;
			}
			ENDHLSL
		}
	}
}
