Shader "Hidden/BlitAdd"
{	

	SubShader
	{
		// Pass 0 - Blit add into result
		Pass
		{
			Cull Off ZWrite Off ZTest Off

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment blendFrag
			#pragma target 4.0

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

			TEXTURE2D(_BlitSource);
			SAMPLER(sampler_BlitSource);

			TEXTURE2D(_BlitAdd);
			SAMPLER(sampler_BlitAdd);


			half3 blendFrag(v2f i) : SV_Target
			{
				return SAMPLE_BASE(_BlitSource, sampler_BlitSource, i.uv) + SAMPLE_BASE(_BlitAdd, sampler_BlitAdd, i.uv);
			}

			ENDHLSL
		}
	}
}
