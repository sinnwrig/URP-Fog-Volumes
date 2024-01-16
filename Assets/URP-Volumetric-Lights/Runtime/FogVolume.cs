using System;
using System.Collections.Generic;
using System.Linq;
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


    [Header("Culling")]
    public float maxDistance;
    public float fadeDistance;


    [Header("Lighting Calculation")]
    public bool hasLighting;
    public bool hasShadows;
    [Range(1, 64)] public int sampleCount = 16;
    [Range(0.0f, 1.0f)] public float scatteringCoef = 0.5f;
    [Range(0.0f, 1f)] public float extinctionCoef = 0.01f;
    [Range(0.0f, 0.999f)] public float mieG = 0.1f;  

    
    [Header("Noise")]
    public Texture3D noiseTexture;
    public float noiseScale = 0.1f;
    public Vector3 noiseDirection = Vector3.one * 0.1f;
    [Range(0, 1)] public float noiseIntensity = 1.0f;
    [Range(0, 1)] public float noiseIntensityOffset = 0.25f;


    void OnEnable()
    {
        VolumetricFogPass.AddVolume(this);
    }

    void OnDisable()
    {
        VolumetricFogPass.RemoveVolume(this);
    }

    void OnDestroy()
    {
        OnDisable();
    }

    void SetupNoise(CommandBuffer cmd)
    {
        cmd.SetKeyword(VolumetricFogPass.noiseKeyword, noiseTexture != null);
        if (noiseTexture != null)
        {
            cmd.SetGlobalTexture("_NoiseTexture", noiseTexture);
            cmd.SetGlobalVector("_NoiseVelocity", noiseDirection * noiseScale);
            cmd.SetGlobalVector("_NoiseData", new Vector3(noiseScale, noiseIntensity, noiseIntensityOffset));
        }
    }

    const int maxLightCount = 64;

    float[] lightsToShadow = new float[maxLightCount];
    Vector4[] lightPositions  = new Vector4[maxLightCount];
    Vector4[] lightColors = new Vector4[maxLightCount];
    Vector4[] lightAttenuations = new Vector4[maxLightCount];
    Vector4[] spotDirections = new Vector4[maxLightCount];


    void SetupLighting(CommandBuffer cmd, List<NativeLight> lights, int maxLights)
    {
        cmd.SetGlobalVector("_AmbientColor", ambientFogColor);
        cmd.SetGlobalFloat("_IntensityModifier", lightIntensityModifier);
        cmd.SetGlobalInt("_SampleCount", sampleCount);

        cmd.SetGlobalFloat("_MieG", mieG);
        cmd.SetGlobalFloat("_Scattering", scatteringCoef);
        cmd.SetGlobalFloat("_Extinction", extinctionCoef);

        cmd.SetKeyword(VolumetricFogPass.lightingKeyword, hasLighting);
        cmd.SetKeyword(VolumetricFogPass.shadowsKeyword, hasLighting && hasShadows);

        if (hasLighting)
        {
            int lightCount = Math.Min(maxLights, lights.Count);

            cmd.SetGlobalInteger("_LightCount", lightCount);  

            for (int i = 0; i < lightCount; i++)
            {   
                NativeLight light = lights[i];

                lightsToShadow[i] = light.shadowIndex;
                lightPositions[i] = light.position;
                lightColors[i] = light.color;
                lightAttenuations[i] = light.attenuation;
                spotDirections[i] = light.spotDirection;
            }

            // Initialize the shader light arrays
            cmd.SetGlobalFloatArray("_LightToShadowIndices", lightsToShadow);
            cmd.SetGlobalVectorArray("_LightPositions", lightPositions);   
            cmd.SetGlobalVectorArray("_LightColors", lightColors);        
            cmd.SetGlobalVectorArray("_LightAttenuations", lightAttenuations);  
            cmd.SetGlobalVectorArray("_SpotDirections", spotDirections); 
        }
    }


    public void RenderVolume(ref RenderingData renderingData, CommandBuffer cmd, Material material, List<NativeLight> lights, int maxLights)
    {
        Vector3[] boundsPoints = volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder ? ShapeBounds.capsuleCorners : ShapeBounds.cubeCorners;

        Vector4 viewport = ShapeBounds.GetViewportRect(transform.localToWorldMatrix, renderingData.cameraData.camera, boundsPoints);

        if (ShapeBounds.InsideBounds(transform.worldToLocalMatrix, renderingData.cameraData.camera.transform.position, GetBounds()))
            viewport = new Vector4(0, 0, 1, 1);

        cmd.SetGlobalVector("_ViewportRect", viewport);
        cmd.SetGlobalVector("_FogRange", new Vector2(maxDistance, maxDistance - fadeDistance));

        volumeType.SetVolumeKeyword(cmd);

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
            return ShapeBounds.GetCapsuleAABB(transform.localToWorldMatrix);

        // Default to cube
        return ShapeBounds.GetCubeAABB(transform.localToWorldMatrix);
    }
}
