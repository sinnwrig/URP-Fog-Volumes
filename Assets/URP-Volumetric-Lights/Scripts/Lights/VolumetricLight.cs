// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo

using UnityEngine;
using UnityEngine.Rendering;
using System;


[RequireComponent(typeof(Light))]
public partial class VolumetricLight : MonoBehaviour 
{
    private Light _light;
    private Material _material;


    [Range(1, 64)]
    public int SampleCount = 8;
    [Range(0.0f, 1.0f)]
    public float ScatteringCoef = 0.5f;
    [Range(0.0f, 0.1f)]
    public float ExtinctionCoef = 0.01f;
    [Range(0.0f, 1.0f)]
    public float SkyboxExtinctionCoef = 0.9f;
    [Range(0.0f, 0.999f)]
    public float MieG = 0.1f;
    public bool HeightFog = false;
    [Range(0, 0.5f)]
    public float HeightScale = 0.10f;
    public float GroundLevel = 0;
    public bool Noise = false;
    public float NoiseScale = 0.015f;
    public float NoiseIntensity = 1.0f;
    public float NoiseIntensityOffset = 0.3f;
    public Vector2 NoiseVelocity = new Vector2(3.0f, 3.0f);   
    public float MaxRayLength = 400.0f;    


    public void Setup(Shader volumetricLight) 
    {
        _light = GetComponent<Light>();
        _material = new Material(volumetricLight);
    }


    private void OnEnable()
    {
        VolumetricLightPass.AddVolumetricLight(this);
    }


    private void OnDisable()
    {
        VolumetricLightPass.RemoveVolumetricLight(this);
    }


    private void OnDestroy()
    {        
        VolumetricLightPass.RemoveVolumetricLight(this);
        DestroyImmediate(_material);
    }


    private void SetMaterialProperties(VolumetricLightPass pass)
    {
        _material.SetVector("_CameraForward", Camera.current.transform.forward);
        _material.SetInt("_SampleCount", SampleCount);

        _material.SetVector("_NoiseVelocity", new Vector4(NoiseVelocity.x, NoiseVelocity.y) * NoiseScale);
        _material.SetVector("_NoiseData", new Vector4(NoiseScale, NoiseIntensity, NoiseIntensityOffset));

        _material.SetVector("_MieG", new Vector4(1 - (MieG * MieG), 1 + (MieG * MieG), 2 * MieG, 1.0f / (4.0f * Mathf.PI)));
        _material.SetVector("_VolumetricLight", new Vector4(ScatteringCoef, ExtinctionCoef, _light.range, 1.0f - SkyboxExtinctionCoef));

        pass.LightPassBuffer.SetGlobalTexture("_CameraDepthTexture", pass.VolumeLightDepthBuffer);


        if (HeightFog)
        {
            _material.EnableKeyword("HEIGHT_FOG");
            _material.SetVector("_HeightFog", new Vector4(GroundLevel, HeightScale));
        }
        else
        {
            _material.DisableKeyword("HEIGHT_FOG");
        }


        if (Noise)
            _material.EnableKeyword("NOISE");
        else
            _material.DisableKeyword("NOISE");
    }


    public void PreRenderEvent(VolumetricLightPass pass)
    {
        // Light was destroyed without deregistring, deregister and destroy component now
        if (_light == null)
        {
            OnDisable();
            DestroyImmediate(this);
        }

        if (!_light.enabled)
        {
            return;
        }

        SetMaterialProperties(pass);

        switch (_light.type)
        {
            case LightType.Point:
                SetupPointLight(pass);
            break;

            case LightType.Spot:
                SetupSpotLight(pass);
            break;

            case LightType.Directional:
                SetupDirectionalLight(pass);
            break;
        }
    }


    private bool HasShadows() 
    {
        if (_light.type == LightType.Directional) 
        {
            return _light.shadows != LightShadows.None;
        }

        bool hasShadows = (_light.transform.position - Camera.current.transform.position).magnitude <= QualitySettings.shadowDistance;

        return hasShadows && _light.shadows != LightShadows.None;
    }
}
