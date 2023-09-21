// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo


Shader "Hidden/VolumetricLight"
{
	Properties
	{
		[HideInInspector]_ZTest ("ZTest", Float) = 0
	}
	
	SubShader
	{
		Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
		LOD 100

		HLSLINCLUDE

#define DEBUG_COLOR
		
		#include "/Include/Common.hlsl"
		#include "/Include/Math.hlsl"

		TEXTURE3D(_NoiseTexture);
		SAMPLER(sampler_NoiseTexture);

		TEXTURE2D(_DitherTexture);
		SAMPLER(sampler_DitherTexture);

		TEXTURE2D(_CameraDepthTexture);
		SAMPLER(sampler_CameraDepthTexture);



		struct appdata
		{
			float4 vertex : POSITION;
			float4 uv : TEXCOORD0;
		};


		float3 _CameraForward;
		float3 _LightColor;
		float3 _LightPos;

		float4 _VolumetricLight; // x: scattering coef, y: extinction coef, z: range w: skybox extinction coef
        float4 _MieG; // x: 1 - g^2, y: 1 + g^2, z: 2*g, w: 1/4pi

		float4 _NoiseData; // x: scale, y: intensity, z: intensity offset
		float4 _NoiseVelocity; // x: x velocity, y: z velocity

		float4 _HeightFog; // x:  ground level, y: height scale, z: unused, w: unused
		float _MaxRayLength;
		int _SampleCount;

		float4x4 _WorldViewProj;


		struct v2f
		{
			float4 pos : SV_POSITION;
			float4 uv : TEXCOORD0;
			float3 viewVector : TEXCOORD2;
		};


		v2f vert(appdata v)
		{
			v2f output;
			output.pos = TransformObjectToHClip(v.vertex.xyz);
			output.uv = v.uv;
			float3 viewVector = mul(unity_CameraInvProjection, float4(v.uv.xy * 2 - 1, 0, -1)).xyz;
			output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

			return output;
		}

		
		//-----------------------------------------------------------------------------------------
		// GetLightAttenuation
		//-----------------------------------------------------------------------------------------
		float GetLightAttenuation(float3 wpos)
		{
			float atten = 0;

#if defined (DIRECTIONAL)
			atten = 1;
	#if defined (SHADOWS_DEPTH)
			// URP Directional light shadows
	#endif

#elif defined (SPOT)	
			atten = 1;
			// URP Spot light attenuation

	#if defined(SHADOWS_DEPTH)
			// URP Spot light shadows
	#endif

#elif defined (POINT)
			atten = 1;
			// URP Point Light attenuation or sumth
	#if defined(SHADOWS_CUBE)
			// URP Point Light shdows
	#endif
#endif
			return atten;
		}

        //-----------------------------------------------------------------------------------------
        // ApplyHeightFog
        //-----------------------------------------------------------------------------------------
        void ApplyHeightFog(float3 wpos, inout float density)
        {
#ifdef HEIGHT_FOG
            density *= exp(-(wpos.y + _HeightFog.x) * _HeightFog.y);
#endif
        }

        //-----------------------------------------------------------------------------------------
        // GetDensity
        //-----------------------------------------------------------------------------------------
		float GetDensity(float3 wpos)
		{
            float density = 1;
#ifdef NOISE
			float noise = SAMPLE_TEXTURE3D(_NoiseTexture, sampler_NoiseTexture, frac(wpos * _NoiseData.x + float3(_Time.y * _NoiseVelocity.x, 0, _Time.y * _NoiseVelocity.y)));
			noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
			density = saturate(noise);
#endif
            ApplyHeightFog(wpos, density);

            return density;
		}        

		//-----------------------------------------------------------------------------------------
		// MieScattering
		//-----------------------------------------------------------------------------------------
		float MieScattering(float cosAngle, float4 g)
		{
            return g.w * (g.x / (pow(g.y - g.z * cosAngle, 1.5)));			
		}

		//-----------------------------------------------------------------------------------------
		// RayMarch
		//-----------------------------------------------------------------------------------------
		float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength)
		{
			float2 interleavedPos = (fmod(floor(screenPos.xy), 8.0));
			float offset = SAMPLE_TEXTURE2D(_DitherTexture, sampler_DitherTexture, interleavedPos / 8.0 + float2(0.5 / 8.0, 0.5 / 8.0)).w;

			int stepCount = _SampleCount;

			float stepSize = rayLength / stepCount;
			float3 step = rayDir * stepSize;

			float3 currentPosition = rayStart + step * offset;

			float4 vlight = 0;

			float cosAngle;
#if defined (DIRECTIONAL)
            float extinction = 0;
			cosAngle = dot(_LightDir.xyz, -rayDir);
#else
			// we don't know about density between camera and light's volume, assume 0.5
			float extinction = length(_WorldSpaceCameraPos - currentPosition) * _VolumetricLight.y * 0.5;
#endif
			[loop]
			for (int i = 0; i < stepCount; ++i)
			{
				float atten = GetLightAttenuation(currentPosition);
				float density = GetDensity(currentPosition);

                float scattering = _VolumetricLight.x * stepSize * density;
				extinction += _VolumetricLight.y * stepSize * density;// +scattering;

				float4 light = atten * scattering * exp(-extinction);

#if !defined (DIRECTIONAL)
				// phase function for spot and point lights
                float3 tolight = normalize(currentPosition - _LightPos.xyz);
                cosAngle = dot(tolight, -rayDir);
				light *= MieScattering(cosAngle, _MieG);
#endif         

				vlight += light;

				currentPosition += step;				
			}

#if defined (DIRECTIONAL)
			// apply phase function for dir light
			vlight *= MieScattering(cosAngle, _MieG);
#endif

			// apply light's color
			vlight.xyz *= _LightColor;

			vlight = max(0, vlight);
#if defined (DIRECTIONAL) // use "proper" out-scattering/absorption for dir light 
			vlight.w = exp(-extinction);
#else
            vlight.w = 0;
#endif

#ifdef DEBUG_COLOR
			return float4(1, 1, 1, 1);
#endif


			return vlight;
		}

		ENDHLSL

		// pass 5 - cone test
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment fragDir
			#pragma target 4.0

			#include "/Include/Intersection.hlsl"

			TEXTURE2D(_SceneColor);
			SAMPLER(sampler_SceneColor);

			float3x4 _Sphere;
			float3x4 _Cylinder;
			float3x4 _Cone;
			float3x4 _Box;

			float3 _DiskPos;
			float3 _DiskNormal;
			float _DiskRadius;


			void Fade(inout float4 sceneColor, float near, float far, float linearDepth)
			{
				if (min(near, far) < linearDepth)
					sceneColor += (float4)min(linearDepth, far) - max(near, 0.0);
			}


			half4 fragDir(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;

				float len = length(i.viewVector);
				float3 rayDir = i.viewVector / len;				

				float3 rayStart = _WorldSpaceCameraPos;

				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_EYE_DEPTH(depth) * len;
				half4 scene = SAMPLE_TEXTURE2D(_SceneColor, sampler_SceneColor, uv);


				float near, far;
				if (RaySphere(_Sphere, rayStart, rayDir, near, far))
					Fade(scene, near, far, linearDepth);
				 
				if (RayCylinder(_Cylinder, rayStart, rayDir, near, far))
					Fade(scene, near, far, linearDepth);

				if (RayCone(_Cone, rayStart, rayDir, near, far))
					Fade(scene, near, far, linearDepth);
				
				if (RayCube(_Box, rayStart, rayDir, near, far))
					Fade(scene, near, far, linearDepth);

				return scene;
			}

			ENDHLSL
		}
	}
}
