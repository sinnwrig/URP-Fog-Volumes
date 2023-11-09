Shader "Unlit/TestShader"
{
    Properties
    {
        _Color ("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent"  "RenderType" = "Transparent" }
        LOD 100
        ZWrite Off
        Blend One One


        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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


            half4 _Color;


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return _Color;
            }

            ENDHLSL
        }
    }
}
