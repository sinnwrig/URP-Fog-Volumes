using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[ExecuteAlways]
public partial class FogVolume : MonoBehaviour 
{
    [Header("Appearance")]
    public VolumeType volumeType;
    public Color ambientFogColor = Color.white;
    public float lightIntensityModifier = 1;
    public Vector3 edgeFade;


    [Header("Culling")]
    public float maxDistance = 100.0f;
    public float fadeDistance = 15.0f;


    [Header("Lighting")]
    public bool hasLighting = true;
    public bool hasShadows = false;
    [Range(1, 64)] public int sampleCount = 16;
    [Range(0, 1)] public float jitterStrength = 0.05f;
    [Range(0.0f, 1.0f)] public float scatteringCoef = 0.5f;
    [Range(0.0f, 1f)] public float extinctionCoef = 0.05f;
    [Range(0.0f, 0.999f)] public float mieG = 0.1f;  

    
    [Header("Noise")]
    public Texture3D noiseTexture;
    public float scale = 0.1f;
    public Vector3 noiseScroll = new Vector3(0, -0.15f, 0);
    [Range(0, 1)] public float noiseIntensity = 1;
    [Range(0, 1)] public float intensityOffset = 0.5f;


    private const int maxLightCount = 64;
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


    void SetupNoise(CommandBuffer cmd)
    {
        cmd.SetKeyword(VolumetricFogPass.noiseKeyword, noiseTexture != null);
        if (noiseTexture != null)
        {
            cmd.SetGlobalTexture("_NoiseTexture", noiseTexture);
            cmd.SetGlobalVector("_NoiseVelocity", (-noiseScroll) * scale);
            cmd.SetGlobalVector("_NoiseData", new Vector3(scale, noiseIntensity, intensityOffset));
        }
    }


    // Upload the list of affecting lights to the shader
    void SetupLighting(CommandBuffer cmd, List<NativeLight> lights, int maxLights)
    {
        cmd.SetGlobalVector("_AmbientColor", ambientFogColor);
        cmd.SetGlobalVector("_EdgeFade", edgeFade);
        cmd.SetGlobalFloat("_IntensityModifier", lightIntensityModifier);
        cmd.SetGlobalInt("_SampleCount", sampleCount);

        cmd.SetGlobalFloat("_MieG", mieG);
        cmd.SetGlobalFloat("_Scattering", scatteringCoef);
        cmd.SetGlobalFloat("_Extinction", extinctionCoef);

        cmd.SetKeyword(VolumetricFogPass.lightingKeyword, hasLighting);
        cmd.SetKeyword(VolumetricFogPass.shadowsKeyword, hasLighting && hasShadows);

        if (hasLighting)
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
        cmd.SetGlobalVector("_FogRange", new Vector2(maxDistance, maxDistance - fadeDistance));
        cmd.SetGlobalFloat("_Jitter", jitterStrength);

        volumeType.SetVolumeKeyword(cmd);

        SetupViewport(cmd, ref renderingData);
        SetupNoise(cmd);
        SetupLighting(cmd, lights, maxLights);

        cmd.DrawMesh(MeshUtility.FullscreenMesh, transform.localToWorldMatrix, material, 0, 1);
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
