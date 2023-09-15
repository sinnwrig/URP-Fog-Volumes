#pragma once

#include "Math.hlsl"

// Code from https://www.shadertoy.com/view/4s23DR

bool sphere(float3 sphereCenter, float sphereRadius, float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	float b = dot(rayDir, rayOrigin);
	float c = dot(rayOrigin, rayOrigin) - 0.25;
	float delta = b * b - c;

	if (delta < 0.0)
    {
        near = -MAX_FLOAT;
        far = MAX_FLOAT;
		return false;
    }

	float deltasqrt = sqrt(delta);
	near = -b - deltasqrt;
	far = -b + deltasqrt;

	return far > 0.0;
}

bool cylinder(float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	// quadratic x^2 + y^2 = 0.5^2 => (rayOrigin.x + t*rayDir.x)^2 + (rayOrigin.y + t*rayDir.y)^2 = 0.5
	float a = dot(rayDir.xy, rayDir.xy);
	float b = dot(rayOrigin.xy, rayDir.xy);
	float c = dot(rayOrigin.xy, rayOrigin.xy) - 0.25;

	float delta = b * b - a * c;
	if(delta < 0.0)
		return false;

	// 2 roots
	float deltasqrt = sqrt(delta);
	float arcp = 1.0 / a;
	near = (-b - deltasqrt) * arcp;
	far = (-b + deltasqrt) * arcp;
	
	// order roots
	float temp = min(far, near);
	far = max(far, near);
	near = temp;

	float znear = rayOrigin.z + near * rayDir.z;
	float zfar = rayOrigin.z + far * rayDir.z;

	// top, bottom
	vec2 zcap = vec2(0.5, -0.5);
	vec2 cap = (zcap - rayOrigin.z) / rayDir.z;

	if ( znear < zcap.y )
		near = cap.y;
	else if ( znear > zcap.x )
		near = cap.x;

	if ( zfar < zcap.y )
		far = cap.y;
	else if ( zfar > zcap.x )
		far = cap.x;
	
	return far > 0.0 && far > near;
}

// cone inscribed in a unit cube centered at 0
bool cone(float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	// scale and offset into a unit cube
	rayOrigin.x += 0.5;
	float s = 0.5;
    
	rayOrigin.x *= s;
	rayDir.x *= s;
	
	// quadratic x^2 = y^2 + z^2
	float a = rayDir.y * rayDir.y + rayDir.z * rayDir.z - rayDir.x * rayDir.x;
	float b = rayOrigin.y * rayDir.y + rayOrigin.z * rayDir.z - rayOrigin.x * rayDir.x;
	float c = rayOrigin.y * rayOrigin.y + rayOrigin.z * rayOrigin.z - rayOrigin.x * rayOrigin.x;
	
	float cap = (s - rayOrigin.x) / rayDir.x;
	
	// linear
	if( a == 0.0 )
	{
		near = -0.5 * c/b;
		float x = rayOrigin.x + near * rayDir.x;
		if( x < 0.0 || x > s )
			return false; 

		far = cap;
		float temp = min(far, near); 
		far = max(far, near);
		near = temp;
		return far > 0.0;
	}

	float delta = b * b - a * c;
	if( delta < 0.0 )
		return false;

	// 2 roots
	float deltasqrt = sqrt(delta);
	float arcp = 1.0 / a;
	near = (-b - deltasqrt) * arcp;
	far = (-b + deltasqrt) * arcp;
	
	// order roots
	float temp = min(far, near);
	far = max(far, near);
	near = temp;

	float xnear = rayOrigin.x + near * rayDir.x;
	float xfar = rayOrigin.x + far * rayDir.x;

	if( xnear < 0.0 )
	{
		if( xfar < 0.0 || xfar > s )
			return false;
		
		near = far;
		far = cap;
	}
	else if( xnear > s )
	{
		if( xfar < 0.0 || xfar > s )
			return false;
		
		near = cap;
	}
	else if( xfar < 0.0 )
	{
		// The apex is problematic,
		// additional checks needed to
		// get rid of the blinking tip here.
		far = near;
		near = cap;
	}
	else if( xfar > s )
	{
		far = cap;
	}
	
	return far > 0.0;
}

// cube() by Simon Green
bool cube(float3 rayOrigin, float3 rayDir, out float near, out float far)
{
	// compute intersection of ray with all six bbox planes
	float3 invR = 1.0/rayDir;
	float3 tbot = invR * (-0.5 - rayOrigin);
	float3 ttop = invR * (0.5 - rayOrigin);
	
	// re-order intersections to find smallest and largest on each axis
	float3 tmin = min (ttop, tbot);
	float3 tmax = max (ttop, tbot);
	
	// find the largest tmin and the smallest tmax
	vec2 t0 = max(tmin.xx, tmin.yz);
	near = max(t0.x, t0.y);
	t0 = min(tmax.xx, tmax.yz);
	far = min(t0.x, t0.y);

	// check for hit
	return near < far && far > 0.0;
}