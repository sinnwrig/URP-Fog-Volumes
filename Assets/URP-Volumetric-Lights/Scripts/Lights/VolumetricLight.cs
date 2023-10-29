// Original project by Michal Skalsky, under the BSD license 
// Modified by Kai Angulo

using UnityEngine;
using UnityEngine.Rendering;
using System;


[RequireComponent(typeof(Light)), ExecuteAlways]
public partial class VolumetricLight : MonoBehaviour 
{
    private Light _light;
    public Light Light
    {
        get 
        {
            if (_light == null)
            {
                _light = GetComponent<Light>();
            }

            return _light;
        }
    }



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
    public Vector2 NoiseVelocity = new(3.0f, 3.0f);   
    public float MaxRayLength = 400.0f;    



    public float coneAngle = 30.0f;
    public float coneLength = 1.0f;



    private void SetMaterialProperties(Material material)
    {
        material.SetInt("_SampleCount", SampleCount);

        material.SetVector("_NoiseVelocity", new Vector4(NoiseVelocity.x, NoiseVelocity.y) * NoiseScale);
        material.SetVector("_NoiseData", new Vector4(NoiseScale, NoiseIntensity, NoiseIntensityOffset));

        material.SetVector("_MieG", new Vector4(1 - (MieG * MieG), 1 + (MieG * MieG), 2 * MieG, 1.0f / (4.0f * Mathf.PI)));
        material.SetVector("_VolumetricLight", new Vector4(ScatteringCoef, ExtinctionCoef, _light.range, 1.0f - SkyboxExtinctionCoef));
    }


    public void DrawLight(Material volumetricMaterial, VolumetricLightPass pass)
    {
        // Light was destroyed without deregistring, deregister and destroy component now
        if (Light == null)
        {
            DestroyImmediate(this);
            return;
        }

        if (!Light.enabled)
        {
            return;
        }


        SetMaterialProperties(volumetricMaterial);

        switch (Light.type)
        {
            case LightType.Point:
                SetupPointLight(volumetricMaterial, pass);
            break;

            case LightType.Spot:
                SetupSpotLight(volumetricMaterial, pass);
            break;

            case LightType.Directional:
                SetupDirectionalLight(volumetricMaterial, pass);
            break;
        }
    }


    private bool HasShadows() 
    {
        if (Light.type == LightType.Directional) 
        {
            return Light.shadows != LightShadows.None;
        }

        bool hasShadows = (Light.transform.position - Camera.current.transform.position).magnitude <= QualitySettings.shadowDistance;

        return hasShadows && Light.shadows != LightShadows.None;
    }
}
