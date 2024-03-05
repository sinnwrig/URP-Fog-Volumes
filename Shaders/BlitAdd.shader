Shader "Hidden/BlitAdd"
{	

	SubShader
	{
		// Pass 0 - Blit add into result
		Pass
		{
			Cull Off ZWrite Off ZTest Off

			HLSLPROGRAM

			#pragma exclude_renderers d3d11_9x
    		#pragma exclude_renderers d3d9

			#pragma vertex vert
			#pragma fragment blendFrag
			#pragma target 4.0

			#include "/Include/Common.hlsl"
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
				o.vertex = CorrectUV(v.vertex);
				o.uv = v.uv;
				return o;
			}


			TEXTURE2D(_BlitSource);
			SAMPLER(sampler_BlitSource);

			TEXTURE2D(_BlitAdd);
			SAMPLER(sampler_BlitAdd);


			half3 blendFrag(v2f i) : SV_Target
			{
				half3 base = SAMPLE_BASE(_BlitSource, sampler_BlitSource, i.uv);
				half4 add = SAMPLE_BASE(_BlitAdd, sampler_BlitAdd, i.uv);

				float srcFactor = 1 - saturate(add.w);

				return (base * srcFactor) + add;
			}

			ENDHLSL
		}
	}
}
