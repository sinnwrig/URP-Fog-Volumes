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


#endif