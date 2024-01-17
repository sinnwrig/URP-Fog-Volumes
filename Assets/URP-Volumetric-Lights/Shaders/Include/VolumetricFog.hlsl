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
	float2 uv : TEXCOORD1;
};


#if defined(NOISE_ENABLED)
	TEXTURE3D(_NoiseTexture);
	SAMPLER(sampler_NoiseTexture);
	float4 _NoiseData; // x: scale, y: intensity, z: intensity offset
	float3 _NoiseVelocity; // noise move direction
#endif


TEXTURE2D_X(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);

half3 _AmbientColor;
float _IntensityModifier;

int _SampleCount;
float _MinStepDistance;
float _Jitter;

float _Scattering;
float _Extinction;
float _MieG;

float4 _ViewportRect;
float _MaxRayLength;
float2 _FogRange;

float4 _EdgeFade;


float FadeBoxEdge(float3 worldPosition)
{
	float3 localPos = mul(UNITY_MATRIX_I_M, float4(worldPosition, 1.0));

	float edgeX = min(localPos.x + 0.5, 0.5 - localPos.x) / abs(_EdgeFade.x);
	float edgeY = min(localPos.y + 0.5, 0.5 - localPos.y) / abs(_EdgeFade.y);
    float edgeZ = min(localPos.z + 0.5, 0.5 - localPos.z) / abs(_EdgeFade.z);

	return saturate(min(min(edgeX, edgeY), edgeZ));
}


float FadeSphereEdge(float3 worldPosition)
{
	float3 localPos = mul(UNITY_MATRIX_I_M, float4(worldPosition, 1.0));
	return saturate((0.5 - length(localPos)) / (abs(_EdgeFade.x) + EPSILON));
}


float FadeCylinderEdge(float3 worldPosition)
{
	float3 localPos = mul(UNITY_MATRIX_I_M, float4(worldPosition, 1.0));
	float radFade = saturate((0.5 - length(localPos.xz)) / (abs(_EdgeFade.x) + EPSILON)) ;
	float edgeY = min(localPos.y + 0.5, 0.5 - localPos.y) / abs(_EdgeFade.y);

	return saturate(min(radFade, edgeY));
}


float FadeCapsuleEdge(float3 worldPosition)
{	
	float3 localPos = mul(UNITY_MATRIX_I_M, float4(worldPosition, 1.0));

	const float3 ab = float3(0, 1, 0);
	float3 ac = localPos - float3(0, -0.5, 0);
	float3 bc = localPos - float3(0, 0.5, 0);

    float e = dot(ac, ab);

	float dist;

    // Handle cases where c projects outside ab
    if (e <= 0.0f) 
		dist = dot(ac, ac);
	else
	{
		float f = dot(ab, ab);
		dist = e >= f ? dot(bc, bc) : dot(ac, ac) - e * e / f;
	}

	return saturate((0.5 - sqrt(dist)) / (abs(_EdgeFade.x) + EPSILON));
}



float GetDensity(float3 worldPosition, float distance)
{
    float density = 1.0;

	#if defined(NOISE_ENABLED)
		float3 samplePos = worldPosition * _NoiseData.x + _Time.y * _NoiseVelocity;

		float noise = SAMPLE_BASE3D(_NoiseTexture, sampler_NoiseTexture, samplePos).x;
		noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
		density = saturate(noise);
	#endif

	#if defined(CUBE_VOLUME)
		density *= FadeBoxEdge(worldPosition);
	#elif defined(CAPSULE_VOLUME)
		density *= FadeCapsuleEdge(worldPosition);
	#elif defined(CYLINDER_VOLUME)
		density *= FadeCylinderEdge(worldPosition);
	#else
		density *= FadeSphereEdge(worldPosition);
	#endif
    
	// Fade density as position gets further from camera
    return density * smoothstep(_FogRange.x, _FogRange.y, distance);
}        


void CalculateSteps(float rayLength, out int count, out float size)
{
	count = _SampleCount;
	size = rayLength / count;
}



half3 RayMarch(float3 rayStart, float3 rayDir, float rayLength, float3 cameraPos)
{
	float cameraDistance = length(cameraPos - rayStart);

	int stepCount;
	float stepSize;
	CalculateSteps(rayLength, stepCount, stepSize);

	float extinction = cameraDistance * _Extinction * 0.5; // Assume density of 0.5 between camera and light

	half3 vlight = 0;
	float distance = 0;

	[loop]
	for (int i = 0; i < stepCount; ++i)
	{
		float3 currentPosition = rayStart + rayDir * distance;

		half3 light = _AmbientColor;
		
		// Additive lighting
		light += GetLightAttenuationMie(currentPosition, rayDir, _MieG) * _IntensityModifier;

		float density = GetDensity(currentPosition, distance + cameraDistance);

        float scattering = _Scattering * stepSize * density;
		extinction += _Extinction * stepSize * density;

		light *= scattering * exp(-extinction);

		vlight += light;
		distance += stepSize;				
	}

	vlight = max(0, vlight);

	return vlight;
}



half3 CalculateVolumetricLight(float3 cameraPos, float3 viewDir, float linearDepth, float2 uv)
{
	bool hit = false;
    float near = 0;
    float far = _FogRange.x;

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
    float3 rayStart = cameraPos + viewDir * (near + Rand(uv) * _Jitter);

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

	clip01 = min(max(clip01, _ViewportRect.xy), _ViewportRect.xy + _ViewportRect.zw);
	output.uv = clip01;

	output.vertex.xy = clip01 * 2 - 1;

	#if UNITY_UV_STARTS_AT_TOP
		output.vertex.y *= -1;
	#endif

	// Get view vector using UV
	float3 viewVector = mul(unity_CameraInvProjection, float4(output.uv * 2 - 1, 0, -1)).xyz;
	
	// Transform to world space
	output.viewVector = mul(unity_CameraToWorld, float4(viewVector, 0)).xyz;

	return output;
}


half3 VolumetricFragment(Varyings i) : SV_Target
{
	float2 uv = i.uv;

	float len = length(i.viewVector);
	float3 rayDir = i.viewVector / len;				

	float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv);
	float linearDepth = LINEAR_EYE_DEPTH(depth) * len;

	half3 light = CalculateVolumetricLight(_WorldSpaceCameraPos.xyz, rayDir, linearDepth, uv);
	return light;
}


