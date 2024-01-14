using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public partial class FogVolume : MonoBehaviour 
{
    public enum VolumeType
    {
        Sphere = 0, 
        Cube = 1,
        Capsule = 2, 
        Cylinder = 3, 
    }


    public static void SetVolumeKeyword(VolumeType type, CommandBuffer cmd)
    {
        for (int i = 0; i < 4; i++)
            cmd.SetKeyword(VolumetricFogPass.shapeKeywords[i], i == (int)type);
    }



    [Header("Appearance")]
    public VolumeType volumeType;
    public Color ambientFogColor = Color.white;
    public float edgeFade = 1;
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


    void SetupLighting(CommandBuffer cmd, List<NativeLight> lights, int maxLights)
    {
        cmd.SetGlobalInt("_SampleCount", sampleCount);

        cmd.SetGlobalFloat("_MieG", mieG);
        cmd.SetGlobalFloat("_Scattering", scatteringCoef);
        cmd.SetGlobalFloat("_Extinction", extinctionCoef);

        cmd.SetKeyword(VolumetricFogPass.lightingKeyword, hasLighting);
        cmd.SetKeyword(VolumetricFogPass.shadowsKeyword, hasLighting && hasShadows);

        if (hasLighting)
        {
            // Initialize the shader light arrays
        }
    }


    public void RenderVolume(ref RenderingData renderingData, CommandBuffer cmd, Material material, List<NativeLight> lights, int maxLights)
    {
        cmd.SetGlobalVector("_FogRange", new Vector2(maxDistance, fadeDistance));

        SetVolumeKeyword(volumeType, cmd);

        SetupNoise(cmd);
        SetupLighting(cmd, lights, maxLights);

        cmd.DrawMesh(MeshUtility.CubeMesh, transform.localToWorldMatrix, material, 0, 1);
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



    void OnDrawGizmosSelected()
    {
        Handles.color = Color.gray;

        Bounds bounds = GetBounds();

        Bounds aabb = GetAABB();

        Handles.DrawWireCube(aabb.center, aabb.size);

        Matrix4x4 trsMatrix = transform.localToWorldMatrix;
        Handles.matrix = trsMatrix;
        Handles.DrawWireCube(bounds.center, bounds.size);

        Handles.color = Color.white;

        switch (volumeType)
        {
            case VolumeType.Sphere:
                HandleExtensions.DrawWireSphere(trsMatrix);
            break;

            case VolumeType.Cube:
                HandleExtensions.DrawWireCube(trsMatrix);
            break;

            case VolumeType.Capsule:
                HandleExtensions.DrawWireCapsule(trsMatrix);
            break;

            case VolumeType.Cylinder:
                HandleExtensions.DrawWireCylinder(trsMatrix);
            break;
        }
    }
}
