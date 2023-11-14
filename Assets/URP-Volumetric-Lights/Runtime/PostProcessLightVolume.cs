using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[System.Serializable]
public class LightVolume : VolumeComponent, IPostProcessComponent
{
    public FloatParameter intensityModifier = new FloatParameter(1);
    public FloatParameter directionalIntensityModifier = new FloatParameter(1);

    public Vector3Parameter noiseDirection = new Vector3Parameter(Vector3.one * 0.1f);
    public FloatParameter noiseIntensity = new FloatParameter(1);
    public FloatParameter noiseIntensityOffset = new ClampedFloatParameter(0.25f, 0, 1);


    public bool IsActive()
    {
        return true; 
    }

    public bool IsTileCompatible()
    {
        return false; 
    }
}

