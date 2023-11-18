using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[System.Serializable, VolumeComponentMenuForRenderPipeline("Light Volume Overrides", typeof(UniversalRenderPipeline))]
public class LightVolume : VolumeComponent
{
    [Header("Intensity Overrides")]
    public FloatParameter directionalIntensityModifier = new FloatParameter(1, true);
    public FloatParameter intensityModifier = new FloatParameter(1, true);

    [Header("Noise")]
    public FloatParameter noiseScale = new FloatParameter(0.1f);
    public Vector3Parameter noiseDirection = new Vector3Parameter(Vector3.one * 0.1f);
    public FloatParameter noiseIntensity = new ClampedFloatParameter(1, 0, 1);
    public FloatParameter noiseIntensityOffset = new ClampedFloatParameter(0.25f, 0, 1);
}

 