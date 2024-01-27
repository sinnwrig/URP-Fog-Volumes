#pragma once

// Original intersection functions from https://www.shadertoy.com/view/4s23DR

// Added matrices to allow transforming the intersection domains.

// Most intersection tests use this parameter format: 

// invTransform: the inverse transform matrix of the object.
// rayOrigin: the origin of the intersection ray.
// rayDir: the normalized direction of the intersection ray.
// out near: the distance along the ray to the object if intersection ocurred.
// out far: the distance along the ray through the object if intersection ocurred.


// Transform ray to object space.
void TransformRay(float3x4 invTransform, inout float3 rayOrigin, inout float3 rayDirection)
{
    rayOrigin = mul(invTransform, float4(rayOrigin, 1.0));
    rayDirection = mul(invTransform, float4(rayDirection, 0.0));
}


// Returns raw ray/sphere intersection
bool RaySphere(float3 spherePos, float sphereRad, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	rayOrigin -= spherePos;

	// quadratic x^2 + y^2 = 0.5^2 => (rayOrigin.x + t*rayDir.x)^2 + (rayOrigin.y + t*rayDir.y)^2 = 0.5
	float a = dot(rayDir, rayDir);
	float b = dot(rayOrigin, rayDir);
	float c = dot(rayOrigin, rayOrigin) - (sphereRad * sphereRad);

	float delta = b * b - a * c;

	// Early discard before square root
	if (delta < 0.0)
		return false;

	float deltasqrt = sqrt(delta);
	float arcp = 1.0 / a;

	near = (-b - deltasqrt) * arcp;
	far = (-b + deltasqrt) * arcp;

	near = max(near, 0.0);
	return far > 0.0;
}


// Returns ray/sphere intersection with a transform
bool RaySphere(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
    // Transform ray into sphere space
	TransformRay(invTransform, rayOrigin, rayDir);
	return RaySphere(0, 0.5, rayOrigin, rayDir, near, far);
}


// Returns raw ray/cylinder intersection
bool RayCylinder(float2 caps, float radius, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	// quadratic x^2 + y^2 = 0.5^2 => (rayOrigin.x + t*rayDir.x)^2 + (rayOrigin.y + t*rayDir.y)^2 = 0.5

	// Only use x-y to orient cylinder in the z axis
	float a = dot(rayDir.xz, rayDir.xz);
	float b = dot(rayOrigin.xz, rayDir.xz);
	float c = dot(rayOrigin.xz, rayOrigin.xz) - (radius * radius);

	float delta = b * b - a * c;

	// Early discard before square root
	if (delta < 0.0)
		return false;

	// 1 root
	float deltasqrt = sqrt(delta);
	float arcp = 1.0 / a;
	near = (-b - deltasqrt) * arcp;
	far = (-b + deltasqrt) * arcp;

	// Order roots
	float temp = min(far, near);
	far = max(far, near);
	near = temp;

	float znear = rayOrigin.y + near * rayDir.y;
	float zfar = rayOrigin.y + far * rayDir.y;

	// Top, Bottom
	float2 zcap = caps;
	float2 cap = (zcap - rayOrigin.y) / rayDir.y;

	// Range tests
	if (znear < zcap.y)
		near = cap.y;
	else if (znear > zcap.x)
		near = cap.x;

	if (zfar < zcap.y)
		far = cap.y;
	else if (zfar > zcap.x)
		far = cap.x;
	
	near = max(near, 0.0);
	return far > 0.0 && far > near;
}


// Returns ray/cylinder intersection with a transform
bool RayCylinder(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	// Transform ray into cylinder space
    TransformRay(invTransform, rayOrigin, rayDir);

	return RayCylinder(float2(1, -1), 0.5, rayOrigin, rayDir, near, far);
}


// Returns ray/cone intersection with a transform
bool RayCone(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{   
    TransformRay(invTransform, rayOrigin, rayDir);

	const float s = 0.5;
	
	rayOrigin.z *= s;
	rayDir.z *= s;
	
	// quadratic x^2 = y^2 + z^2
	float a = rayDir.y * rayDir.y + rayDir.x * rayDir.x - rayDir.z * rayDir.z;
	float b = rayOrigin.y * rayDir.y + rayOrigin.x * rayDir.x - rayOrigin.z * rayDir.z;
	float c = rayOrigin.y * rayOrigin.y + rayOrigin.x * rayOrigin.x - rayOrigin.z * rayOrigin.z;

	// Cap on z axis
	float cap = (s - rayOrigin.z) / rayDir.z;
	
	// Linear
	if (a == 0.0)
	{
		near = -0.5 * c / b;
		float z = rayOrigin.z + near * rayDir.z;

		if (z < 0.0 || z > s)
			return false; 

		far = cap;
		float temp = min(far, near); 
		far = max(far, near);

		near = max(temp, 0.0);
		return far > 0.0;
	}

	float delta = b * b - a * c;

	if (delta < 0.0)
		return false;

	// 1 root
	float deltasqrt = sqrt(delta);
	float arcp = 1.0 / a;
	near = (-b - deltasqrt) * arcp;
	far = (-b + deltasqrt) * arcp;
	
	// Order roots
	float temp = min(far, near);
	far = max(far, near);
	near = temp;

	float znear = rayOrigin.z + near * rayDir.z;
	float zfar = rayOrigin.z + far * rayDir.z;

	if (znear < 0.0)
	{
		if (zfar < 0.0 || zfar > s)
			return false;

		near = far;
		far = cap;
	}
	else if (znear > s)
	{
		if (zfar < 0.0 || zfar > s)
			return false;

		near = cap;
	}
	else if(zfar < 0.0)
	{
		// The apex is problematic, additional checks needed to get rid of the blinking tip here.
		far = near;
		near = cap;
	}
	else if (zfar > s)
		far = cap;
	
	near = max(near, 0.0);
	return far > 0.0;
}


// Returns ray/capsule intersection with a transform
bool RayCapsule(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	TransformRay(invTransform, rayOrigin, rayDir);

	near = MAX_FLOAT;
	far = -MAX_FLOAT;

	float s1n, s1f;
	bool s1Hit = RaySphere(float3(0, 0.5, 0), 0.5, rayOrigin, rayDir, s1n, s1f);

	float s2n, s2f;
	bool s2Hit = RaySphere(float3(0, -0.5, 0), 0.5, rayOrigin, rayDir, s2n, s2f);

	float c1n, c1f;
	bool cylHit = RayCylinder(float2(0.5, -0.5), 0.5, rayOrigin, rayDir, c1n, c1f);

	if (s1Hit)
	{
		near = min(s1n, near);
		far = max(s1f, far);
	}

	if (s2Hit)
	{
		near = min(s2n, near);
		far = max(s2f, far);
	}

	if (cylHit)
	{
		near = min(c1n, near);
		far = max(c1f, far);
	}

	return s1Hit || s2Hit || cylHit;
}


// Returns ray/cube intersection with a transform
bool RayCube(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
    TransformRay(invTransform, rayOrigin, rayDir);

	float3 p = -rayOrigin / rayDir;
    float3 q = 0.5 / abs(rayDir);
	float3 tmin = p - q;
	float3 tmax = p + q;
    near = max(tmin.x, max(tmin.y,tmin.z));
	far = min(tmax.x, min(tmax.y,tmax.z));

	near = max(near, 0.0);
	return near < far && far > 0.0;
}


// Returns ray/plane intersection
bool RayPlane(float3 planePos, float3 planeNormal, float3 rayOrigin, float3 rayDir, out float distance)
{
	float denom = dot(planeNormal, rayDir);
	distance = MAX_FLOAT;

    if (denom < 1e-6) 
	{
        float3 pl = planePos - rayOrigin;
      	distance = dot(pl, planeNormal) / denom; 
	
		return distance >= 0;
    }	

    return false;
}


// Returns ray/disk intersection
bool RayDisk(float3 diskPos, float3 diskNormal, float diskRadius, float3 rayOrigin, float3 rayDir, out float distance)
{
	if (RayPlane(diskPos, diskNormal, rayOrigin, rayDir, distance))
	{
		// Dumb and simple distance check
		float3 wPos = rayOrigin + (rayDir * distance);
		float3 distVec = wPos - diskPos;

		if (dot(distVec, distVec) < diskRadius * diskRadius)
			return true;
	}

	distance = MAX_FLOAT;
	return false;
}