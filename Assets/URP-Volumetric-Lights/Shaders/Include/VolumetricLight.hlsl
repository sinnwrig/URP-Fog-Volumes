TEXTURE3D(_NoiseTexture);
SAMPLER(sampler_NoiseTexture);

TEXTURE2D(_DitherTexture);
SAMPLER(sampler_DitherTexture);


float3 _LightColor;
float3 _LightPos;

float4 _VolumetricLight; // x: scattering coef, y: extinction coef, z: range w: skybox extinction coef
float4 _MieG; // x: 1 - g^2, y: 1 + g^2, z: 2*g, w: 1/4pi

float3 _NoiseData; // x: scale, y: intensity, z: intensity offset
float2 _NoiseVelocity; // x: x velocity, y: z velocity

float _MaxRayLength;
int _SampleCount;

float3 _LightDir;
float2 _LightRange;
float3x4 _InvLightMatrix;

		
// TODO: Get URP light index and shadow index, shove it in shader, and use builtins for light attenuations
float GetLightAttenuation(float3 wpos)
{
	float atten = 1;

	return atten;
}


float GetDensity(float3 wpos)
{
    float density = 1;

#ifdef NOISE
	float noise = SAMPLE_TEXTURE3D(_NoiseTexture, sampler_NoiseTexture, frac(wpos * _NoiseData.x + float3(_Time.y * _NoiseVelocity.x, 0, _Time.y * _NoiseVelocity.y)));
	noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
	density = saturate(noise);
#endif
    
    return density;
}        


float MieScattering(float cosAngle, float4 g)
{
    return g.w * (g.x / (pow(g.y - g.z * cosAngle, 1.5)));			
}


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

#ifdef DIRECTIONAL_LIGHT
    float extinction = 0;
	cosAngle = dot(_LightDir.xyz, -rayDir);
#else
	// we don't know about density between camera and light's volume, assume 0.5
	float extinction = length(_WorldSpaceCameraPos.xyz - currentPosition) * _VolumetricLight.y * 0.5;
#endif

	[unroll(MAX_STEPS)]
	for (int i = 0; i < stepCount; ++i)
	{
		float atten = GetLightAttenuation(currentPosition);
		float density = GetDensity(currentPosition);

        float scattering = _VolumetricLight.x * stepSize * density;
		extinction += _VolumetricLight.y * stepSize * density;

		float4 light = atten * scattering * exp(-extinction);

    #ifndef DIRECTIONAL_LIGHT
		// phase function for spot and point lights
        float3 tolight = normalize(currentPosition - _LightPos.xyz);
        cosAngle = dot(tolight, -rayDir);
		light *= MieScattering(cosAngle, _MieG);
    #endif         

		vlight += light;

		currentPosition += step;				
	}

#ifdef DIRECTIONAL_LIGHT
	// apply phase function for dir light
	vlight *= MieScattering(cosAngle, _MieG);
#endif

	// apply light's color
	vlight.xyz *= _LightColor;
	vlight = max(0, vlight);
	

#ifdef DIRECTIONAL_LIGHT // use "proper" out-scattering/absorption for dir light 
    vlight.w = exp(-extinction);
#else
    vlight.w = 0;
#endif

	return vlight;
}



float4 CalculateVolumetricLight(float4 source, float2 uv, float3 cameraPos, float3 viewDir, float linearDepth)
{
    bool hit = false;
    float near = 0;
    float far = MAX_FLOAT;

#ifdef POINT_LIGHT
    hit = RaySphere(_InvLightMatrix, cameraPos, viewDir, near, far);
#else
    #ifdef SPOT_LIGHT
        hit = RayCone(_InvLightMatrix, cameraPos, viewDir, near, far);
    #else
        #ifdef DIRECTIONAL_LIGHT 
            hit = true; 
			far = 0.25; 
        #endif
    #endif
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

	//return RayMarch(uv, rayStart, viewDir, rayLength);
		
	
	if (far > _LightRange.x)
		return source;

	float fade = far - max(near, 0.0);

	fade *= 1 - smoothstep(_LightRange.x - _LightRange.y, _LightRange.x, far);

	fade *= 0.1;

	return source + (float4)fade;
}


