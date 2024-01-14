#pragma once

#include "Math.hlsl"

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


// Returns ray/sphere intersection.
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


// Returns ray/sphere intersection.
bool RaySphere(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
    // Transform ray into sphere space
	TransformRay(invTransform, rayOrigin, rayDir);
	return RaySphere(0, 0.5, rayOrigin, rayDir, near, far);
}


// Returns ray/cylinder intersection.
bool RayCylinder(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	// Transform ray into cylinder space
    TransformRay(invTransform, rayOrigin, rayDir);

	// quadratic x^2 + y^2 = 0.5^2 => (rayOrigin.x + t*rayDir.x)^2 + (rayOrigin.y + t*rayDir.y)^2 = 0.5

	// Only use x-y to orient cylinder in the z axis
	float a = dot(rayDir.xz, rayDir.xz);
	float b = dot(rayOrigin.xz, rayDir.xz);
	float c = dot(rayOrigin.xz, rayOrigin.xz) - 0.25;

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
	float2 zcap = float2(1, -1);
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


// Returns ray/cone intersection.
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


// Ray-Capsule intersection
bool RayCapsule(float3x4 invTransform, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	TransformRay(invTransform, rayOrigin, rayDir);

	// http://pastebin.com/2XrrNcxb

	// Substituting equ. (1) - (6) to equ. (I) and solving for t' gives:
	//
	// t' = (t * dot(AB, d) + dot(AB, AO)) / dot(AB, AB); (7) or
	// t' = t * m + n where 
	// m = dot(AB, d) / dot(AB, AB) and 
	// n = dot(AB, AO) / dot(AB, AB)
	//
	
	const float radius = 1;
	const float3 pointA = float3(0, -2, 0);
	const float3 pointB = float3(0, 2, 0);

	float3 AB = pointB - pointA;
	float3 AO = rayOrigin - pointA;

	float AB_dot_d = dot(rayDir, rayDir);
	float AB_dot_AO = dot(AO, AO);
	float AB_dot_AB = dot(AB, AB);
	
	float m = AB_dot_d / AB_dot_AB;
	float n = AB_dot_AO / AB_dot_AB;

	// Substituting (7) into (II) and solving for t gives:
	//
	// dot(Q, Q)*t^2 + 2*dot(Q, R)*t + (dot(R, R) - r^2) = 0
	// where
	// Q = d - AB * m
	// R = AO - AB * n
	float3 Q = rayDir - (AB * m);
	float3 R = AO - (AB * n);

	float a = dot(Q, Q);
	float b = 2.0f * dot(R, R);
	float c = dot(R, R) - (radius * radius);

	if (a == 0.0f)
	{
		// Special case: AB and ray direction are parallel. If there is an intersection it will be on the end spheres...
		// NOTE: Why is that?
		// Q = d - AB * m =>
		// Q = d - AB * (|AB|*|d|*cos(AB,d) / |AB|^2) => |d| == 1.0
		// Q = d - AB * (|AB|*cos(AB,d)/|AB|^2) =>
		// Q = d - AB * cos(AB, d) / |AB| =>
		// Q = d - unit(AB) * cos(AB, d)
		//
		// |Q| == 0 means Q = (0, 0, 0) or d = unit(AB) * cos(AB,d)
		// both d and unit(AB) are unit vectors, so cos(AB, d) = 1 => AB and d are parallel.
		// 

		float atmin, atmax, btmin, btmax;
		if(!RaySphere(pointA, radius, rayOrigin, rayDir, atmin, atmax) || !RaySphere(pointB, radius, rayOrigin, rayDir, atmin, atmax))
			return false;

		near = min(atmin, btmin);
		far = max(atmax, btmax);

		return true;
	}

	float discriminant = b * b - 4.0f * a * c;

	if (discriminant < 0.0f)
	{
		// The ray doesn't hit the infinite cylinder defined by (A, B).
		// No intersection.
		return false;
	}

	float rsqr = rsqrt(discriminant);
	float tmin = (-b - (rsqr)) / (2.0f * a);
	float tmax = (-b + (rsqr)) / (2.0f * a);

	if (tmin > tmax)
	{
		float temp = tmin;
		tmin = tmax;
		tmax = temp;
	}

	// Now check to see if K1 and K2 are inside the line segment defined by A,B
	float t_k1 = tmin * m + n;

	if (t_k1 < 0.0f)
	{
		float stmin, stmax;

		if (!RaySphere(pointA, radius, rayOrigin, rayDir, stmin, stmax))
			return false;

		near = stmin;
	}
	else if (t_k1 > 1.0f)
	{
		float stmin, stmax;

		if (!RaySphere(pointB, radius, rayOrigin, rayDir, stmin, stmax))
			return false;

		near = stmin;
	}
	else
	{
		near = tmin;
	}

	float t_k2 = tmax * m + n;

	if (t_k2 < 0.0f)
	{
		float stmin, stmax;
		if (!RaySphere(pointA, radius, rayOrigin, rayDir, stmin, stmax))
			return false;
		
		far = stmax;
	}
	else if (t_k2 > 1.0f)
	{
		float stmin, stmax;

		if (!RaySphere(pointB, radius, rayOrigin, rayDir, stmin, stmax))
			return false;

		far = stmax;
	}
	else
	{
		far = tmax;
	}

	return true;
}


// Returns ray/cube intersection.
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


// Returns ray/plane intersection.

// planePos: the world position of the plane.
// planeNormal: the world normal of the plane.
// rayOrigin: the origin of the intersection ray.
// rayDir: the normalized direction of the intersection ray.
// out distance: the distance along {rayDir} to the plane.

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


// Returns ray/disk intersection.

// diskPos: the world position of the disk.
// diskNormal: the world normal of the disk.
// diskRadius: the radius of the disk.
// rayOrigin: the origin of the intersection ray.
// rayDir: the normalized direction of the intersection ray.
// out distance: the distance along {rayDir} to the disk.

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