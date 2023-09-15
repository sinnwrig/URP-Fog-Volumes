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
			float3 wpos : TEXCOORD1;
			float3 viewVector : TEXCOORD2;
		};


		// From UnityCG.cginc
		float4 ComputeScreenPos(float4 pos) {
		    float4 o = pos * 0.5f;
		    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
		    o.zw = pos.zw;
		    return o;
		}


		v2f vert(appdata v)
		{
			v2f o;
			o.pos = TransformObjectToHClip(v.vertex.xyz);

			o.uv = ComputeScreenPos(o.pos);
			o.wpos = TransformObjectToWorld(v.vertex.xyz);

			float3 viewVector = mul(unity_CameraInvProjection, float4(o.uv.xy * 2 - 1, 0, -1)).xyz;
			o.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

			return o;
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


		// pass 0 - point light, camera inside
		Pass
		{
			Cull Front ZWrite Off ZTest Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment fragPointInside
			#pragma target 4.0

			#pragma shader_feature HEIGHT_FOG
			#pragma shader_feature NOISE
			#pragma shader_feature SHADOWS_CUBE
			#pragma shader_feature POINT_COOKIE
			#pragma shader_feature POINT
						

			
			half4 fragPointInside(v2f i) : SV_Target
			{	
				float2 uv = i.uv.xy / i.uv.w;

				// read depth and reconstruct world position
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);			

				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.wpos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir);

				rayDir /= rayLength;

				float linearDepth = LINEAR_EYE_DEPTH(depth);
				float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
				rayLength = min(rayLength, projectedDepth);
				
				return RayMarch(i.pos.xy, rayStart, rayDir, rayLength);
			}
			ENDHLSL
		}


		// pass 1 - spot light, camera inside
		Pass
		{
			Cull Front ZWrite Off ZTest Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment fragPointInside
			#pragma target 4.0

			#pragma shader_feature HEIGHT_FOG
			#pragma shader_feature NOISE
			#pragma shader_feature SHADOWS_DEPTH
			#pragma shader_feature SPOT



			half4 fragPointInside(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy / i.uv.w;

				// read depth and reconstruct world position
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);

				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.wpos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir);

				rayDir /= rayLength;

				float linearDepth = LINEAR_EYE_DEPTH(depth);
				float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
				rayLength = min(rayLength, projectedDepth);

				return RayMarch(i.pos.xy, rayStart, rayDir, rayLength);
			}
			ENDHLSL
		}


		// pass 2 - point light, camera outside
		Pass
		{
			Cull Back ZWrite Off ZTest Always

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment fragPointOutside
			#pragma target 4.0

			#pragma shader_feature HEIGHT_FOG
			#pragma shader_feature SHADOWS_CUBE
			#pragma shader_feature NOISE

			#pragma shader_feature POINT_COOKIE
			#pragma shader_feature POINT



			half4 fragPointOutside(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy / i.uv.w;

				// read depth and reconstruct world position
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
			
				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.wpos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir);

				rayDir /= rayLength;

				float3 lightToCamera = _WorldSpaceCameraPos - _LightPos;

				float b = dot(rayDir, lightToCamera);
				float c = dot(lightToCamera, lightToCamera) - (_VolumetricLight.z * _VolumetricLight.z);

				float d = sqrt((b*b) - c);
				float start = -b - d;
				float end = -b + d;

				float linearDepth = LINEAR_EYE_DEPTH(depth);
				float projectedDepth = linearDepth / dot(_CameraForward, rayDir);
				end = min(end, projectedDepth);

				rayStart = rayStart + rayDir * start;
				rayLength = end - start;

				return RayMarch(i.pos.xy, rayStart, rayDir, rayLength);
			}
			ENDHLSL
		}
				
		// pass 3 - spot light, camera outside
		Pass
		{ 
			Cull Back ZWrite Off ZTest Always

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment fragSpotOutside
			#pragma target 4.0

			#pragma shader_feature HEIGHT_FOG
			#pragma shader_feature SHADOWS_DEPTH
			#pragma shader_feature NOISE
			#pragma shader_feature SPOT

			
			float _CosAngle;
			float4 _ConeAxis;
			float4 _ConeApex;
			float _PlaneD;


			half4 fragSpotOutside(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy / i.uv.w;

				// read depth and reconstruct world position
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);

				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayEnd = i.wpos;

				float3 rayDir = (rayEnd - rayStart);
				float rayLength = length(rayDir);

				rayDir /= rayLength;

				// inside cone
				float3 r1 = rayEnd + rayDir * 0.001;

				bool intr;
				// plane intersection	
				float planeCoord = RayPlaneIntersect(_ConeAxis, _PlaneD, r1, rayDir, intr);

				// ray cone intersection
				float2 lineCoords = RayConeIntersect(_ConeApex, _ConeAxis, _CosAngle, r1, rayDir);

				float linearDepth = LINEAR_EYE_DEPTH(depth);
				float projectedDepth = linearDepth / dot(_CameraForward, rayDir);

				float z = (projectedDepth - rayLength);
				rayLength = min(planeCoord, min(lineCoords.x, lineCoords.y));
				rayLength = min(rayLength, z);

				return RayMarch(i.pos.xy, rayEnd, rayDir, rayLength);
			}
			ENDHLSL
		}		

		// pass 4 - directional light
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment fragDir
			#pragma target 4.0

			#pragma shader_feature HEIGHT_FOG
			#pragma shader_feature NOISE
			#pragma shader_feature SHADOWS_DEPTH
			#pragma shader_feature DIRECTIONAL_COOKIE
			#pragma shader_feature DIRECTIONAL


			half4 fragDir(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;
				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_01_DEPTH(depth);
				
				float3 rayStart = _WorldSpaceCameraPos;
				float3 rayDir = normalize(i.viewVector);				
				rayDir *= linearDepth;

				float rayLength = length(rayDir);
				rayDir /= rayLength;

				rayLength = min(rayLength, _MaxRayLength);

				float4 color = RayMarch(i.pos.xy, rayStart, rayDir, rayLength);

				if (linearDepth > 0.999999)
				{
					color.w = lerp(color.w, 1, _VolumetricLight.w);
				}
				
				return color;
			}
			ENDHLSL
		}

		// pass 5 - cone test
		Pass
		{
			Cull Off ZWrite Off ZTest Always

			HLSLPROGRAM

			#pragma vertex vert
			#pragma fragment fragDir
			#pragma target 4.0


			TEXTURE2D(_SceneColor);
			SAMPLER(sampler_SceneColor);


			float3 _ConeApex;
			float3 _ConeAxis;
			float _PlaneDist;
			
			float _CosAngle;
			float _BaseRadius;



			half4 fragDir(v2f i) : SV_Target
			{
				float2 uv = i.uv.xy;

				float len = length(i.viewVector);
				float3 rayDir = i.viewVector / len;				

				float3 rayStart = _WorldSpaceCameraPos;


				bool insideCone;
				// ray cone intersection
				float2 lineCoords = RayConeIntersect(_ConeApex, _ConeAxis, _CosAngle, _BaseRadius, _PlaneDist, rayStart, rayDir, insideCone);

				float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
				float linearDepth = LINEAR_EYE_DEPTH(depth) * len;

				float3 col = insideCone ? float3(0.5, 0.5, 0.5) : float3(0.25, 0.75, 0.5);

				float rayLength = min(lineCoords.x, lineCoords.y);

				half4 scene = SAMPLE_TEXTURE2D(_SceneColor, sampler_SceneColor, uv);

				if (rayLength > 0.0 && rayLength < linearDepth)
				{
					scene = half4(col, 1.0);
				}

				return scene;
			}

			ENDHLSL
		}
	}
}
