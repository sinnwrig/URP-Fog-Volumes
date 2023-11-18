// Original project by Michal Skalsky under the BSD license 
// Modified by Kai Angulo

#pragma once


struct Attributes
{
	float4 vertex : POSITION;
	float2 uv : TEXCOORD0;
};


struct Varyings
{
	float4 vertex : SV_POSITION;
	float3 worldPos : TEXCOORD0;
	float2 uv : TEXCOORD1;
};


#if defined(NOISE)
	TEXTURE3D(_NoiseTexture);
	SAMPLER(sampler_NoiseTexture);
	float3 _NoiseData; // x: scale, y: intensity, z: intensity offset
	float3 _NoiseVelocity; // noise move direction
#endif

TEXTURE2D_X(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

int _SampleCount;
float2 _VolumetricLight; // x: scattering coef, y: extinction coef
float _MieG;

float _MaxRayLength;

float2 _LightRange;

float4 _ViewportRect;


float GetDensity(float3 worldPosition, float distance)
{
    float density = 1.0;

#if defined(NOISE)
	float3 samplePos = worldPosition * _NoiseData.x + _Time.y * _NoiseVelocity;

	float noise = SAMPLE_BASE3D(_NoiseTexture, sampler_NoiseTexture, samplePos).x;
	noise = saturate(noise - _NoiseData.z);

	density *= lerp(1.0, noise, _NoiseData.y);
#endif
    
	// Fade density as position gets further from camera
    return density * smoothstep(_LightRange.y, _LightRange.x, distance);
}        


float MiePhase(float cosAngle)
{
	float gSqr = _MieG * _MieG;

	// Magic number is 1/4pi
    return (0.07957747154) * ((1 - gSqr) / (pow(abs((1 + gSqr) - (2 * _MieG) * cosAngle), 1.5)));
}


float4 RayMarch(float3 rayStart, float3 rayDir, float rayLength, float3 cameraPos)
{
	float cameraDistance = length(cameraPos - rayStart);

	int stepCount = _SampleCount;
	float stepSize = rayLength / stepCount;

#if defined(DIRECTIONAL_LIGHT)
    float extinction = 0;
#else
	float extinction = cameraDistance * _VolumetricLight.y * 0.5; // Assume density of 0.5 between camera and light
#endif

	float4 vlight = 0;
	float distance = 0;

	[loop]
	for (int i = 0; i < stepCount; ++i)
	{
		float3 currentPosition = rayStart + rayDir * distance;

		// Attenuated light color
		float4 attenuatedLight = GetLightAttenuation(currentPosition);
		float density = GetDensity(currentPosition, distance + cameraDistance);

        float scattering = _VolumetricLight.x * stepSize * density;
		extinction += _VolumetricLight.y * stepSize * density;

		float4 light = attenuatedLight * scattering * exp(-extinction);

		// Apply mie phase to light
        float3 toLight = -normalize(_LightPosition.xyz - currentPosition * _LightPosition.w);
		light *= MiePhase(dot(toLight, -rayDir));     

		vlight += light;

		distance += stepSize;				
	}

	vlight = max(0, vlight);	

#if defined(DIRECTIONAL_LIGHT) // use "proper" out-scattering/absorption for dir light 
    vlight.w = exp(-extinction);
#else
    vlight.w = 1;
#endif

	return vlight;
}

float3x4 _SpotLight2;


float4 CalculateVolumetricLight(float3x4 invLightMatrix, float3 cameraPos, float3 viewDir, float linearDepth)
{
    bool hit = false;
    float near = 0;
    float far = MAX_FLOAT;

#if defined(POINT_LIGHT)
    hit = RaySphere(invLightMatrix, cameraPos, viewDir, near, far);
#elif defined(SPOT_LIGHT)
    hit = RayCone(invLightMatrix, cameraPos, viewDir, near, far);
#elif defined(DIRECTIONAL_LIGHT)
    hit = true; 
	far = _MaxRayLength;
#endif

    // No intersection
    if (!hit)
        return 0;	
    
    far = min(far, linearDepth);
    float rayLength = (far - near);

    // Object is behind scene depth
    if (rayLength < 0)
        return 0;

    // Jump to point on intersection surface
    float3 rayStart = cameraPos + viewDir * near;

	return RayMarch(rayStart, viewDir, rayLength, cameraPos);
}


Varyings VolumetricVertex(Attributes v)
{
	Varyings output = (Varyings)0;
	output.vertex = v.vertex;

#if UNITY_UV_STARTS_AT_TOP
	output.vertex.y *= -1;
#endif

	float2 clip01 = output.vertex.xy * 0.5 + 0.5;

	// Only clip when camera is far enough from light to prevent invalid clipping when inside light
	float3 distVec = _WorldSpaceCameraPos.xyz - _LightPosition.xyz;
    if (dot(distVec, distVec) > 0.5)
	{
		// Clamp clip space position to inside of light viewport rect
		clip01 = min(max(clip01, _ViewportRect.xy), _ViewportRect.xy + _ViewportRect.zw);
	}

	output.uv = clip01;

	output.vertex.xy = clip01 * 2 - 1;

#if UNITY_UV_STARTS_AT_TOP
	output.vertex.y *= -1;
#endif

	// Get view vector using UV
	float3 viewVector = mul(unity_CameraInvProjection, float4(output.uv * 2 - 1, 0, -1)).xyz;
	// Transform to world space
	output.worldPos = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

	return output;
}


half4 VolumetricFragment(Varyings i) : SV_Target
{
	float2 uv = i.uv.xy;

	float len = length(i.worldPos);
	float3 rayDir = i.worldPos / len;				

	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
	float linearDepth = LINEAR_EYE_DEPTH(depth) * len;

	// Use inverse transform matrix for light
	return CalculateVolumetricLight(UNITY_MATRIX_I_M, _WorldSpaceCameraPos.xyz, rayDir, linearDepth);
}


