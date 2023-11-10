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

TEXTURE2D(_DitherTexture);
SAMPLER(sampler_DitherTexture);

TEXTURE2D_X(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

int _SampleCount;
float2 _VolumetricLight; // x: scattering coef, y: extinction coef
float3 _MieG; // x: 1 - g^2, y: 1 + g^2, z: 2*g

float _MaxRayLength;

float2 _LightRange;

float4 _ViewportRect;


float GetDensity(float3 wpos, float distance)
{
    float density = 1;

	// Fade density as position gets further from camera
	float distanceFade = smoothstep(_LightRange.y, _LightRange.x, distance);

#if defined(NOISE)
	float noise = SAMPLE_BASE3D(_NoiseTexture, sampler_NoiseTexture, frac(wpos * _NoiseData.x + _Time.y * _NoiseVelocity)).x;
	noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
	density = saturate(noise);
#endif
    
    return density * distanceFade;
}        


float MieScattering(float cosAngle)
{
	float3 g = _MieG;
	// Magic number is 1/4pi
    return (0.07957747154) * (g.x / (pow(abs(g.y - g.z * cosAngle), 1.5)));
}


float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength, float3 cameraPos)
{
	float2 interleavedPos = (fmod(floor(screenPos.xy), 8.0));
	float offset = SAMPLE_BASE(_DitherTexture, sampler_DitherTexture, interleavedPos / 8.0 + (float2)(0.5 / 8.0)).w;

	int stepCount = _SampleCount;
	float stepSize = rayLength / stepCount;


	float3 step = rayDir * stepSize;

	float3 currentPosition = rayStart + step * offset;

	float4 vlight = 0;

#if defined(DIRECTIONAL_LIGHT)
    float extinction = 0;
#else
	// we don't know about density between camera and light's volume, assume 0.5
	float extinction = length(_WorldSpaceCameraPos.xyz - currentPosition) * _VolumetricLight.y * 0.5;
#endif
	float dist = length(cameraPos - currentPosition);

	[loop]
	for (int i = 0; i < stepCount; ++i)
	{
		dist += stepSize;

		// Attenuation but actually just use color
		float4 attenuatedLight = GetLightAttenuation(currentPosition);
		float density = GetDensity(currentPosition, dist);

        float scattering = _VolumetricLight.x * stepSize * density;
		extinction += _VolumetricLight.y * stepSize * density;

		float4 light = attenuatedLight * scattering * exp(-extinction);


		// phase function for spot and point lights
        float3 tolight = -normalize(_LightPosition.xyz - currentPosition * _LightPosition.w);
        float cosAngle = dot(tolight, -rayDir);
		light *= MieScattering(cosAngle);     

		vlight += light;

		currentPosition += step;				
	}

	vlight = max(0, vlight);	

#if defined(DIRECTIONAL_LIGHT) // use "proper" out-scattering/absorption for dir light 
    vlight.w = exp(-extinction);
#else
    vlight.w = 1;
#endif

	// Force 0-1 range
	vlight.w = saturate(vlight.w);

	return vlight;
}



float4 CalculateVolumetricLight(float2 uv, float3x4 invLightMatrix, float3 cameraPos, float3 viewDir, float linearDepth)
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

	// Additive blending
	return RayMarch(uv, rayStart, viewDir, rayLength, cameraPos);
}


Varyings VolumetricVertex(Attributes v)
{
	Varyings output = (Varyings)0;
	output.vertex = v.vertex;

#if UNITY_UV_STARTS_AT_TOP
	output.vertex.y *= -1;
#endif

	float2 clip01 = output.vertex.xy * 0.5 + 0.5;

	clip01 = min(max(clip01, _ViewportRect.xy), _ViewportRect.xy + _ViewportRect.zw);
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
	return CalculateVolumetricLight(uv, UNITY_MATRIX_I_M, _WorldSpaceCameraPos.xyz, rayDir, linearDepth);
}


