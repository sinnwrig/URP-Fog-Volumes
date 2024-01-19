using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[ExecuteAlways]
public partial class FogVolume : MonoBehaviour 
{
    public FogVolumeProfile profile;

    [Header("Appearance")]
    public VolumeType volumeType;
    public Vector3 edgeFade = Vector3.zero;


    [Header("Culling")]
    [Min(0)] public float maxDistance = 100.0f;
    [Range(0, 0.999f)] public float distanceFade = 1.0f;


    // 
    private const int maxLightCount = 32;
    private float[] lightsToShadow = new float[maxLightCount];
    private Vector4[] lightPositions  = new Vector4[maxLightCount];
    private Vector4[] lightColors = new Vector4[maxLightCount];
    private Vector4[] lightAttenuations = new Vector4[maxLightCount];
    private Vector4[] spotDirections = new Vector4[maxLightCount];



    void OnEnable() => VolumetricFogPass.AddVolume(this);
    void OnDisable() => VolumetricFogPass.RemoveVolume(this);
    void OnDestroy() => OnDisable();


    void SetupViewport(CommandBuffer cmd, ref RenderingData renderingData)
    {
        Vector3[] boundsPoints = volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder ? ShapeBounds.capsuleCorners : ShapeBounds.cubeCorners;

        Vector4 viewport = ShapeBounds.GetViewportRect(transform.localToWorldMatrix, renderingData.cameraData.camera, boundsPoints);

        if (ShapeBounds.InsideBounds(transform.worldToLocalMatrix, renderingData.cameraData.camera.transform.position, GetBounds()))
            viewport = new Vector4(0, 0, 1, 1);

        cmd.SetGlobalVector("_ViewportRect", viewport);
    }


    Vector3 EdgeFade()
    {
        switch (volumeType)
        {
            case VolumeType.Cube:
                float edgeX = -((1 - Mathf.Clamp(edgeFade.x, -1, 1)) * 2 - 1) * 0.5f;
                float edgeY = -((1 - Mathf.Clamp(edgeFade.y, -1, 1)) * 2 - 1) * 0.5f;
                float edgeZ = -((1 - Mathf.Clamp(edgeFade.z, -1, 1)) * 2 - 1) * 0.5f;

                return new Vector3(edgeX, edgeY, edgeZ);

            case VolumeType.Cylinder:
                float cylX = -((1 - Mathf.Clamp(edgeFade.x, -1, 1)) * 2 - 1) * 0.5f;
                float cylY = -(1 - (Mathf.Clamp(edgeFade.y, -1, 1) * 2));
                return new Vector3(cylX, cylY, 0);

            default:
                float sphereX = -((1 - Mathf.Clamp(edgeFade.x, -1, 1)) * 2 - 1) * 0.5f;
                return new Vector3(sphereX, 0, 0);
        }
    }


    // Upload the list of affecting lights to the shader
    void SetupLighting(CommandBuffer cmd, List<NativeLight> lights, int maxLights)
    {
        cmd.SetGlobalVector("_EdgeFade", EdgeFade());

        if (profile.hasLighting)
        {
            Bounds bounds = GetBounds();
            Matrix4x4 trs = transform.localToWorldMatrix;
            Matrix4x4 invTrs = transform.worldToLocalMatrix;

            int lightCount = 0;
            for (int i = 0; i < Math.Min(lights.Count, maxLights); i++)
            {   
                NativeLight light = lights[i];

                if (light.isDirectional || ShapeBounds.WithinDistance(trs, invTrs, light.position, light.range, bounds))
                {
                    lightsToShadow[lightCount] = light.shadowIndex;
                    lightPositions[lightCount] = light.position;
                    lightColors[lightCount] = light.color;
                    lightAttenuations[lightCount] = light.attenuation;
                    spotDirections[lightCount] = light.spotDirection;

                    lightCount++;
                }
            }

            cmd.SetGlobalInteger("_LightCount", lightCount);  

            // Initialize the shader light arrays
            cmd.SetGlobalFloatArray("_LightToShadowIndices", lightsToShadow);
            cmd.SetGlobalVectorArray("_LightPositions", lightPositions);   
            cmd.SetGlobalVectorArray("_LightColors", lightColors);        
            cmd.SetGlobalVectorArray("_LightAttenuations", lightAttenuations);  
            cmd.SetGlobalVectorArray("_SpotDirections", spotDirections); 
        }
    }


    public void DrawVolume(ref RenderingData renderingData, CommandBuffer cmd, Material material, List<NativeLight> lights, int maxLights)
    {
        cmd.SetGlobalVector("_FogRange", new Vector2(maxDistance, Mathf.Lerp(0, maxDistance, distanceFade)));

        profile.SetupProperties(cmd);
        volumeType.SetVolumeKeyword(cmd);

        SetupViewport(cmd, ref renderingData);
        SetupLighting(cmd, lights, maxLights);

        cmd.DrawMesh(MeshUtility.FullscreenMesh, transform.localToWorldMatrix, material, 0, 1);
    }
    

    public bool CullVolume(Vector3 cameraPosition, Plane[] cameraPlanes)
    {
        if (profile == null)
            return true;

        Bounds aabb = GetAABB();

        // Volume is past maximum distance
        if ((cameraPosition - aabb.ClosestPoint(cameraPosition)).sqrMagnitude > maxDistance * maxDistance)
            return true;

        // Volume is outside camera frustum
        if (!GeometryUtility.TestPlanesAABB(cameraPlanes, aabb))
            return true;
        
        return false;
    }


    public Bounds GetBounds()
    {
        if (volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder) 
            return ShapeBounds.capsuleBounds;

        // Default to cube
        return ShapeBounds.cubeBounds;
    }


    public Bounds GetAABB()
    {
        if (volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder) 
            return GeometryUtility.CalculateBounds(ShapeBounds.capsuleCorners, transform.localToWorldMatrix);

        // Default to cube
        return GeometryUtility.CalculateBounds(ShapeBounds.cubeCorners, transform.localToWorldMatrix);
    }
}
