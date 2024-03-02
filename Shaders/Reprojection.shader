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

			#include "/Include/Common.hlsl"
			#include "/Include/Math.hlsl"
			#include "/Include/Reprojection.hlsl"

	
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

			TEXTURE2D(_TemporalBuffer);
			SAMPLER(sampler_TemporalBuffer);

			TEXTURE2D(_TemporalTarget);
			SAMPLER(sampler_TemporalTarget);


			half4 reprojectFrag(v2f i) : SV_Target
			{
				return ReprojectPixel(i.uv, 
					TEXTURE2D_ARGS(_TemporalBuffer, sampler_TemporalBuffer), 
					TEXTURE2D_ARGS(_TemporalTarget, sampler_TemporalTarget),
					TEXTURE2D_ARGS(_CameraDepthTexture, sampler_CameraDepthTexture));
			}

			ENDHLSL
		}
	}
}
