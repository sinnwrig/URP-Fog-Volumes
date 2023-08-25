#ifndef CUSTOM_MATH_INCLUDED
#define CUSTOM_MATH_INCLUDED

static const float MATH_PI = 3.14159265359;
static const float MATH_TAU = MATH_PI * 2;
static const float MAX_FLOAT = 3.402823466e+38;
static const float DEG2RAD = (MATH_PI * 2) / 360;
static const float RAD2DEG = 360 / (MATH_PI * 2);


// Remap the components of a vector from one range to another
float4 Remap(float4 v, float minOld, float maxOld, float minNew, float maxNew) 
{
	return saturate(minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld));//
}

// Remap a float value (with a known mininum and maximum) to a value between 0 and 1
float4 Remap01(float4 v, float minOld, float maxOld) 
{
	return saturate((v - minOld) / (maxOld - minOld));
}

// Smooth minimum of two values, controlled by smoothing factor k
// When k = 0, this behaves identically to min(a, b)
float SmoothMin(float a, float b, float k) 
{
	k = max(0, k);
	// https://www.iquilezles.org/www/articles/smin/smin.htm
	float h = max(0, min(1, (b - a + k) / (2 * k)));
	return a * h + b * (1 - h) - k * h * (1 - h);
}

// Smooth maximum of two values, controlled by smoothing factor k
// When k = 0, this behaves identically to max(a, b)
float SmoothMax(float a, float b, float k) 
{
	k = min(0, -k);
	float h = max(0, min(1, (b - a + k) / (2 * k)));
	return a * h + b * (1 - h) - k * h * (1 - h);
}


float Blend(float startHeight, float blendDst, float height) 
{
	return smoothstep(startHeight - blendDst / 2, startHeight + blendDst / 2, height);
}


// Returns vector (dstToSphere, dstThroughSphere)
// If ray origin is inside sphere, dstToSphere = 0
// If ray misses sphere, dstToSphere = maxValue; dstThroughSphere = 0
float2 RaySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) 
{
	float3 offset = rayOrigin - sphereCentre;
	float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
	float b = 2 * dot(offset, rayDir);
	float c = dot(offset, offset) - sphereRadius * sphereRadius;
	float d = b * b - 4 * a * c; // Discriminant from quadratic formula

	float2 intersection = float2(MAX_FLOAT, 0);

	// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
	if (d > 0) {
		float s = sqrt(d);
		float dstToSphereNear = max(0, (-b - s) / (2 * a));
		float dstToSphereFar = (-b + s) / (2 * a);
		// Ignore intersections that occur behind the ray
		if (dstToSphereFar >= 0) {
			intersection = float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
		}
	}


	return intersection;
}


// Returns vector (dstToCone, dstThroughCone)
float2 RayConeIntersect(in float3 f3ConeApex, in float3 f3ConeAxis, in float fCosAngle, in float3 f3RayStart, in float3 f3RayDir)
{
	f3RayStart -= f3ConeApex;
	float a = dot(f3RayDir, f3ConeAxis);
	float b = dot(f3RayDir, f3RayDir);
	float c = dot(f3RayStart, f3ConeAxis);
	float d = dot(f3RayStart, f3RayDir);
	float e = dot(f3RayStart, f3RayStart);
	fCosAngle *= fCosAngle;
	float A = a*a - b*fCosAngle;
	float B = 2 * (c*a - d*fCosAngle);
	float C = c*c - e*fCosAngle;
	float D = B*B - 4 * A*C;

	if (D > 0)
	{
		D = sqrt(D);
		float2 t = (-B + sign(A)*float2(-D, +D)) / (2 * A);
		bool2 b2IsCorrect = c + a * t > 0 && t > 0;
		t = t * b2IsCorrect + !b2IsCorrect * (10000);
		return t;
	}
	else // no intersection
	{
    	return MAX_FLOAT;
    }
}


float RayPlaneIntersect(in float3 planeNormal, in float planeD, in float3 rayOrigin, in float3 rayDir)
{
	float NdotD = dot(planeNormal, rayDir);
	float NdotO = dot(planeNormal, rayOrigin);
	float t = -(NdotO + planeD) / NdotD;
	if (t < 0)
		t = MAX_FLOAT;
	return t;
}


#endif