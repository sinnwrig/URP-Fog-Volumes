Shader "Hidden/TemporalReprojection"
{	
	SubShader
	{
		Pass
		{
			Cull Off ZWrite Off ZTest Off

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment reprojectFrag
			#pragma target 4.0

			#define TEMPORAL_REPROJECTION_ENABLED

			#include "/Include/Common.hlsl"
	
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};


			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};


			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = CorrectVertex(v.vertex);
				o.uv = v.uv;
				return o;
			}


			TEXTURE2D(_CameraDepthTexture);       
			SAMPLER(sampler_CameraDepthTexture);

			TEXTURE2D(_ReprojectSource);
			SAMPLER(sampler_ReprojectSource);

			#include "/Include/Math.hlsl"
			#include "/Include/Reprojection.hlsl"


			half4 reprojectFrag(v2f i) : SV_Target
			{
				return ReprojectPixel(i.uv, TEXTURE2D_ARGS(_ReprojectSource, sampler_ReprojectSource));
			}

			ENDHLSL
		}
	}
}
