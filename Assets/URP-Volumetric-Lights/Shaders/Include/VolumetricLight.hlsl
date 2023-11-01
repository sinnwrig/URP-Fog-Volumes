TEXTURE3D(_NoiseTexture);
SAMPLER(sampler_NoiseTexture);

TEXTURE2D(_DitherTexture);
SAMPLER(sampler_DitherTexture);


float3 _LightPos;

float2 _VolumetricLight; // x: scattering coef, y: extinction coef
float4 _MieG; // x: 1 - g^2, y: 1 + g^2, z: 2*g, w: 1/4pi

float3 _NoiseData; // x: scale, y: intensity, z: intensity offset
float3 _NoiseVelocity; // noise move direction

float _MaxRayLength;
int _SampleCount;

float3 _LightDir;
float2 _LightRange;

float3x4 _InvLightMatrix;


float GetDensity(float3 wpos, float distance)
{
    float density = 1;

	// Fade density as position gets further from camera
	float distanceFade = smoothstep(_LightRange.y, _LightRange.x, distance);

#ifdef NOISE
	// Prevent compiler from using gradient function by specifying mip level
	float noise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, frac(wpos * _NoiseData.x + (_Time.y * _NoiseVelocity)), 0).x;
	noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
	density = saturate(noise);
#endif
    
    return density * distanceFade;
}        


float MieScattering(float cosAngle, float4 g)
{
    return g.w * (g.x / (pow(abs(g.y - g.z * cosAngle), 1.5)));
}


float4 RayMarch(float2 screenPos, float3 rayStart, float3 rayDir, float rayLength, float3 cameraPos)
{
	float2 interleavedPos = (fmod(floor(screenPos.xy), 8.0));
	float offset = SAMPLE_TEXTURE2D_LOD(_DitherTexture, sampler_DitherTexture, interleavedPos / 8.0 + float2(0.5 / 8.0, 0.5 / 8.0), 0).w;

	int stepCount = _SampleCount;

	float stepSize = rayLength / stepCount;
	float3 step = rayDir * stepSize;

	float3 currentPosition = rayStart + step * offset;

	float4 vlight = 0;

#ifdef DIRECTIONAL_LIGHT
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
		light *= MieScattering(cosAngle, _MieG);     

		vlight += light;

		currentPosition += step;				
	}

	vlight = max(0, vlight);	

#ifdef DIRECTIONAL_LIGHT // use "proper" out-scattering/absorption for dir light 
    vlight.w = exp(-extinction);
#else
    vlight.w = 1;
#endif

	// Force 0-1 range
	vlight.w = saturate(vlight.w);

	return vlight;
}



float4 CalculateVolumetricLight(float4 source, float2 uv, float3 cameraPos, float3 viewDir, float linearDepth)
{
    bool hit = false;
    float near = 0;
    float far = MAX_FLOAT;

#if defined(POINT_LIGHT)
    hit = RaySphere(_InvLightMatrix, cameraPos, viewDir, near, far);
#elif defined(SPOT_LIGHT)
    hit = RayCone(_InvLightMatrix, cameraPos, viewDir, near, far);
#elif defined(DIRECTIONAL_LIGHT)
    hit = true; 
	far = _MaxRayLength;
#endif

    // No intersection
    if (!hit)
        return source;	
    
    far = min(far, linearDepth);
    float rayLength = (far - near);

    // Object is behind scene depth
    if (rayLength < 0)
        return source;

    // Jump to point on intersection surface
    float3 rayStart = cameraPos + viewDir * near;

	// Additive blending
	return source + RayMarch(uv, rayStart, viewDir, rayLength, cameraPos);
}


