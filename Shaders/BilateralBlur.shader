Shader "Hidden/BilateralBlur"
{
	HLSLINCLUDE
	
	#include "/Include/Common.hlsl"	
	#include "/Include/Math.hlsl"
	
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
	
	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Off

		// Pass 0 - horizontal blur
		Pass
		{
			Name "Horizontal Blur"

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment horizontalFrag
            #pragma target 4.0
			
			#pragma multi_compile_fragment _ FULL_RES_BLUR HALF_RES_BLUR QUARTER_RES_BLUR

			#include "/Include/BilateralBlur.hlsl"


			TEXTURE2D(_BlurSource);
			SAMPLER(sampler_BlurSource);
			float4 _BlurSource_TexelSize;
			
			
			half4 horizontalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input.uv, TEXTURE2D_ARGS(_BlurSource, sampler_BlurSource), _BlurSource_TexelSize, int2(1, 0));
			}

			ENDHLSL
		}

		// Pass 1 - vertical blur
		Pass
		{
			Name "Vertical Blur"

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment verticalFrag
            #pragma target 4.0

			#pragma multi_compile_fragment _ FULL_RES_BLUR HALF_RES_BLUR QUARTER_RES_BLUR
			
			#include "/Include/BilateralBlur.hlsl"


			TEXTURE2D(_BlurSource);
			SAMPLER(sampler_BlurSource);
			float4 _BlurSource_TexelSize;

			
			half4 verticalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input.uv, TEXTURE2D_ARGS(_BlurSource, sampler_BlurSource), _BlurSource_TexelSize, int2(0, 1));
			}

			ENDHLSL
		}

		// pass 2 - downsample depth
		Pass
		{
			Name "Downsample Depth"

			HLSLPROGRAM
			#pragma vertex DownsampleVertex
			#pragma fragment DownsampleFragment
            #pragma target 4.0

			#include "/Include/Downsample.hlsl"

			ENDHLSL
		}

		// pass 3 - depth aware upsample
		Pass
		{
			Name "Depth Aware Upsample"

			Blend One Zero

			HLSLPROGRAM
			#pragma vertex VertUpsample
			#pragma fragment DepthAwareUpsample	
            #pragma target 4.0

			#include "/Include/Upsample.hlsl"

			ENDHLSL
		}
	}
}
