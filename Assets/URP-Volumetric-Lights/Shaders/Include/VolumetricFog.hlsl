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
	float3 viewVector : TEXCOORD0;
	float4 uv : TEXCOORD1;
};


#if defined(NOISE_ENABLED)
	TEXTURE3D(_NoiseTexture);
	SAMPLER(sampler_NoiseTexture);
	float3 _NoiseData; // x: scale, y: intensity, z: intensity offset
	float3 _NoiseVelocity; // noise move direction
#endif


TEXTURE2D_X(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

int _SampleCount;
float _Scattering;
float _Extinction;
float _MieG;

float _MaxRayLength;

float2 _FogRange;


float GetDensity(float3 worldPosition, float distance)
{
    float density = 1.0;

#if defined(NOISE_ENABLED)
	float3 samplePos = worldPosition * _NoiseData.x + _Time.y * _NoiseVelocity;

	float noise = SAMPLE_BASE3D(_NoiseTexture, sampler_NoiseTexture, samplePos).x;
	noise = saturate(noise - _NoiseData.z);

	density *= lerp(1.0, noise, _NoiseData.y);
#endif
    
	// Fade density as position gets further from camera
    return density * smoothstep(_FogRange.y, _FogRange.x, distance);
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

	float extinction = cameraDistance * _Extinction * 0.5; // Assume density of 0.5 between camera and light

	float4 vlight = 0;
	float distance = 0;

	[loop]
	for (int i = 0; i < stepCount; ++i)
	{
		float3 currentPosition = rayStart + rayDir * distance;

		// Attenuated light color
		float4 attenuatedLight = GetLightAttenuation(currentPosition);
		float density = GetDensity(currentPosition, distance + cameraDistance);

        float scattering = _Scattering * stepSize * density;
		extinction += _Extinction * stepSize * density;

		float4 light = attenuatedLight * scattering * exp(-extinction);

		// Apply mie phase to light
        float3 toLight = -normalize(_LightPosition.xyz - currentPosition * _LightPosition.w);
		light *= MiePhase(dot(toLight, -rayDir));     

		vlight += light;

		distance += stepSize;				
	}

	vlight = max(0, vlight);	
    vlight.w = 1;

	return vlight;
}

float3x4 _SpotLight2;


float4 CalculateVolumetricLight(float3 cameraPos, float3 viewDir, float linearDepth)
{
	//return 1;

	bool hit = false;
    float near = 0;
    float far = MAX_FLOAT;

#if defined(CUBE_VOLUME)
	hit = RayCube(UNITY_MATRIX_I_M, cameraPos, viewDir, near, far);
#elif defined(CAPSULE_VOLUME)
	hit = RayCapsule(UNITY_MATRIX_I_M, cameraPos, viewDir, near, far);
#elif defined(CYLINDER_VOLUME)
	hit = RayCylinder(UNITY_MATRIX_I_M, cameraPos, viewDir, near, far);
#else
	// Default to sphere
	hit = RaySphere(UNITY_MATRIX_I_M, cameraPos, viewDir, near, far);
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

	return (float4)far - max(near, 0.0);// RayMarch(rayStart, viewDir, rayLength, cameraPos);
}



Varyings VolumetricVertex(Attributes v)
{
	Varyings output = (Varyings)0;
		
	float4 clipPos = TransformObjectToHClip(v.vertex);

	output.vertex = clipPos;

	float4 screenUV = ComputeScreenPos(clipPos);

	output.uv = screenUV;

	// Get view vector using UV
	float3 viewVector = mul(unity_CameraInvProjection, float4((screenUV.xy / screenUV.w) * 2 - 1, 0, -1)).xyz;
	// Transform to world space
	output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

	return output;
}


half2 VolumetricFragment(Varyings i) : SV_Target
{
	float2 uv = i.uv.xy / i.uv.w;

	float len = length(i.viewVector);
	float3 rayDir = i.viewVector / len;				

	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
	float linearDepth = LINEAR_EYE_DEPTH(depth) * len;

	// Use inverse transform matrix for light
	return CalculateVolumetricLight(_WorldSpaceCameraPos.xyz, rayDir, linearDepth);
}


