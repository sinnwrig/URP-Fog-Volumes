Shader "Hidden/CorgiGodRays/ScreenSpaceGodRays"
{
    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
    #include "SharedInputs.hlsl"

    #pragma target 5.0

    struct AttributesDefault
    {
        float4 positionHCS : POSITION;
        float2 uv          : TEXCOORD0;
    };

    struct VaryingsDefault
    {
        float4 positionCS  : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    VaryingsDefault VertDefault(AttributesDefault v)
    {
        VaryingsDefault o;

        o.positionCS = float4(v.positionHCS.xyz, 1.0);
        o.uv = v.uv;

        return o;
    }

    float _MainLightScattering;
    float _AdditionalLightScattering;
    float _MainLightIntensity;
    float _AdditionalLightIntensity;


    float ComputeScattering(float lightDotView, float scatterAmount)
    {
        return 1.0;
    }

    float3 GetMainLightContribution(float3 worldPosition, float rayDirDotLightDir)
    {
        #ifndef GODRAYS_MAIN_LIGHT
            return 0.0;
        #endif

        float4 shadowCoord = TransformWorldToShadowCoord(worldPosition);
        float shadowAttenuation = MainLightRealtimeShadow(shadowCoord);
        
        float scatterColor = ComputeScattering(rayDirDotLightDir, _MainLightScattering);
        shadowAttenuation = scatterColor * shadowAttenuation * _MainLightIntensity;
       
        float3 result = shadowAttenuation * _MainLightColor;
        
        #if defined(_LIGHT_COOKIES)
            float3 cookieColor = SampleMainLightCookie(worldPosition);
            result *= cookieColor;
        #endif

        return result;
    }


    float3 GetAdditionalLightContribution(float3 rayDirection, float3 worldPosition)
    {
        float3 contribution = float3(0.0, 0.0, 0.0);

        uint pixelLightCount = GetCorgiLightCount(); 
        CORGI_LIGHT_LOOP_BEGIN(pixelLightCount)
            int perObjectLightIndex = lightIndex;
            int shadowLightIndex = CorgiLightToShadowIndex(lightIndex);

            Light light = GetCorgiAdditionalPerObjectLight(perObjectLightIndex, worldPosition);

            #ifdef GODRAYS_ADDITIVE_LIGHT_SHADOWS
                // directional lights as additional lights were supported in 2021.x.x+ (URP 12+)
                #if UNITY_VERSION >= 2021000
                    light.shadowAttenuation = AdditionalLightRealtimeShadow(shadowLightIndex, worldPosition, light.direction);
                #else
                    light.shadowAttenuation = AdditionalLightRealtimeShadow(shadowLightIndex, worldPosition);
                #endif
            #endif

            #if defined(_LIGHT_COOKIES)
                float3 cookieColor = SampleAdditionalLightCookie(perObjectLightIndex, worldPosition);
                light.color *= cookieColor;
            #endif

            float rayDirDotLightDir = dot(rayDirection, -light.direction);
            float scatter = ComputeScattering(rayDirDotLightDir, _AdditionalLightScattering);

            contribution += light.color * (light.shadowAttenuation * light.distanceAttenuation * scatter * _AdditionalLightIntensity);
        CORGI_LIGHT_LOOP_END

        return contribution;
    }


    float Frag(VaryingsDefault i) : SV_Target
    {
        GetMainLightContribution(0.0, 0.0);
        GetAdditionalLightContribution(0.0, 0.0);
        return 1.0;
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
                #pragma vertex VertDefault
                #pragma fragment Frag
                
                // Universal Pipeline keywords
                // note: screenspace shadows should never be used for this - we need to trace through world space data
                #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
                #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
                #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
                #pragma multi_compile_fragment _ _SHADOWS_SOFT
                #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
                #pragma multi_compile_fragment _ _LIGHT_LAYERS
                #pragma multi_compile_fragment _ _LIGHT_COOKIES

                // Unity keywords
                #pragma multi_compile_instancing
                #pragma multi_compile _ _CLUSTERED_RENDERING
                #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
                #pragma multi_compile _ SHADOWS_SHADOWMASK
                #pragma multi_compile _ DIRLIGHTMAP_COMBINED
                #pragma multi_compile _ LIGHTMAP_ON
                #pragma multi_compile _ DYNAMICLIGHTMAP_ON

                // our settings 
                #pragma multi_compile VOLUME_STEPS_LOW VOLUME_STEPS_MED VOLUME_STEPS_HIGH 
                #pragma multi_compile _ GODRAYS_MAIN_LIGHT
                #pragma multi_compile _ GODRAYS_ADDITIVE_LIGHTS 
                #pragma multi_compile _ GODRAYS_ADDITIVE_LIGHT_SHADOWS
                #pragma multi_compile _ GODRAYS_VARIABLE_INTENSITY 
                #pragma multi_compile _ GODRAYS_ENCODE_LIGHT_COLOR
                #pragma multi_compile _ GODRAYS_DISCARD_TEMPORAL

            ENDHLSL
        }
    }
}