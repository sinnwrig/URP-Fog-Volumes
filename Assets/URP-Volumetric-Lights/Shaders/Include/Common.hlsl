#pragma once

// Defines all the defult unity shader variables and includes commonly-used files.

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float4x4 unity_ObjectToWorld;
float4x4 unity_WorldToObject;
float4x4 unity_PrevObjectToWorldArray;
float4x4 unity_PrevWorldToObjectArray;
real4 unity_WorldTransformParams;

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_PrevMatrixV;
float4x4 glstate_matrix_projection;

float4x4 unity_CameraProjection;
float4x4 unity_CameraInvProjection;
float4x4 unity_CameraToWorld;

float4 _ScreenParams;
float4 _ZBufferParams;
float4 unity_OrthoParams;
float4 _WorldSpaceCameraPos;
float4 _ProjectionParams;

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_PREV_MATRIX_M unity_PrevObjectToWorldArray
#define UNITY_PREV_MATRIX_I_M unity_PrevWorldToObjectArray
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_PrevMatrixV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_MATRIX_P glstate_matrix_projection

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"	
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"


#define LINEAR_EYE_DEPTH(depth) LinearEyeDepth(depth, _ZBufferParams)
#define LINEAR_01_DEPTH(depth) Linear01Depth(depth, _ZBufferParams)