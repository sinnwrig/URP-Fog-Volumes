// Original project copyrighted by Michal Skalsky under the BSD license 
// Modified by Kai Angulo

// NOTE : Have had experiences where Blit() in C# does not properly set _MainTex, so blur source texture is now explicitly set for material

Shader "Hidden/BilateralBlur"
{
	HLSLINCLUDE
	
	#include "/Include/Common.hlsl"	
	
	#pragma multi_compile FULL_RES_BLUR_KERNEL_SIZE
	#pragma multi_compile HALF_RES_BLUR_KERNEL_SIZE
	#pragma multi_compile QUARTER_RES_BLUR_KERNEL_SIZE
	#pragma multi_compile SOURCE_FULL_DEPTH
	
	
	#if defined(FULL_RES_BLUR_KERNEL_SIZE)
		#define KERNEL_SIZE 7
	#elif defined(HALF_RES_BLUR_KERNEL_SIZE)
		#define KERNEL_SIZE 5
	#elif defined(QUARTER_RES_BLUR_KERNEL_SIZE)
		#define KERNEL_SIZE 6
	#else
		#define KERNEL_SIZE 0
	#endif
	
	
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
		o.vertex = TransformObjectToHClip(v.vertex.xyz);
		o.uv = v.uv;
		return o;
	}
	
	ENDHLSL

	SubShader
	{
		Cull Off ZWrite Off ZTest Always

		// Pass 0 - horizontal blur
		Pass
		{
			Name "Horizontal Blur"

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment horizontalFrag
            #pragma target 4.0

			#include "/Include/BilateralBlur.hlsl"

			
			half4 horizontalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(1, 0), KERNEL_SIZE);
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

			#include "/Include/BilateralBlur.hlsl"

			
			half4 verticalFrag(v2f input) : SV_Target
			{
                return BilateralBlur(input, int2(0, 1), KERNEL_SIZE);
			}

			ENDHLSL
		}

		// pass 2 - downsample depth
		Pass
		{
			Name "Downsample Depth"

			HLSLPROGRAM
			#pragma vertex VertDownsampleDepth
			#pragma fragment DownsampleDepth
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
