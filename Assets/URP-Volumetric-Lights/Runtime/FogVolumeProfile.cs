using UnityEngine;
using UnityEngine.Rendering;


[CreateAssetMenu(menuName = "Fog Volumes/Volume Profile")]
public class FogVolumeProfile : ScriptableObject
{
    [Header("Appearance")]
    public Color fogAlbedo = Color.white;
    public float intensity = 1;


    [Header("Raymarching")]
    public Vector2 minMaxStepLength = new Vector2(0.5f, 3f);
    [Range(1, 2)] public float stepIncrementFactor = 1.1f;
    [Min(0)] public float maxRayLength = 50.0f;

    [Range(1, 128)] public int maxSampleCount = 36;
    public float jitterStrength = 0.05f;


    [Header("Lighting")]
    public bool hasLighting = true;
    public bool hasShadows = false;
    [Min(0)] public float lightIntensityModifier = 1;
    [Range(0, 1)] public float scattering = 0.1f;
    [Range(0, 1)] public float extinction = 0.05f;
    [Range(0, 0.999f)] public float mieG = 0.1f;  

    
    [Header("Noise")]
    public Texture3D noiseTexture;
    [Min(0)] public float scale = 0.1f;
    public Vector3 noiseScroll = new Vector3(0, -0.15f, 0);
    [Range(0, 1)] public float noiseIntensity = 1;
    [Range(0, 1)] public float intensityOffset = 0.5f;



    public void SetupProperties(CommandBuffer cmd)
    {
        cmd.SetGlobalFloat("_Jitter", jitterStrength);

        SetupNoise(cmd);
        SetupLighting(cmd);
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


    void SetupLighting(CommandBuffer cmd)
    {
        cmd.SetGlobalVector("_Albedo", fogAlbedo * intensity);

        cmd.SetGlobalFloat("_IntensityModifier", lightIntensityModifier);
        cmd.SetGlobalVector("_StepParams", new Vector4(minMaxStepLength.x, minMaxStepLength.y, stepIncrementFactor, maxRayLength));
        cmd.SetGlobalInt("_MaxSampleCount", maxSampleCount);

        cmd.SetGlobalFloat("_MieG", mieG);
        cmd.SetGlobalFloat("_Scattering", scattering);
        cmd.SetGlobalFloat("_Extinction", extinction);

        cmd.SetKeyword(VolumetricFogPass.lightingKeyword, hasLighting);
        cmd.SetKeyword(VolumetricFogPass.shadowsKeyword, hasLighting && hasShadows);
    }
}
