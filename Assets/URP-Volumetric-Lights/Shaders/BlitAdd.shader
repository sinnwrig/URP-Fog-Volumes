// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo

// NOTE : Have had experiences where Blit() in C# does not properly set _MainTex, so source texture is now explicitly set for material


Shader "Hidden/BlitAdd" 
{
	SubShader
	{
		Pass
		{
			ZTest Always Cull Off ZWrite Off

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "Include/Common.hlsl"

			TEXTURE2D(_Source);
			SAMPLER(sampler_Source);

			TEXTURE2D(_SourceAdd);
			SAMPLER(sampler_SourceAdd);


			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};


			struct v2f 
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
			};



			v2f vert(appdata v)
			{
				v2f o;

				o.vertex = TransformObjectToHClip(v.vertex.xyz);
				o.uv = v.uv;

				return o;
			}


			half4 frag(v2f i) : SV_Target
			{
				float4 source = SAMPLE_TEXTURE2D(_Source, sampler_Source, i.uv);

				float4 sourceAdd = SAMPLE_TEXTURE2D(_SourceAdd, sampler_SourceAdd, i.uv);

				source *= sourceAdd.w;
				source.xyz += sourceAdd.xyz;

				return source;
			}
			ENDHLSL
		}
	}
}
