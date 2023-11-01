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
                _light = GetComponent<Light>();

            return _light;
        }
    }


    [Range(1, 64)] public int sampleCount = 16;
    [Range(0.0f, 1.0f)] public float scatteringCoef = 0.5f;
    [Range(0.0f, 0.1f)] public float extinctionCoef = 0.01f;
    [Range(0.0f, 0.999f)] public float mieG = 0.1f;  


    public int SetShaderProperties(CommandBuffer cmd)
    {
        // Light was destroyed, so component must go too
        if (Light == null)
        {
            DestroyImmediate(this);
            return -1;
        }

        if (!Light.enabled)
        {
            return -1;
        }

        cmd.SetGlobalInt("_SampleCount", sampleCount);

        cmd.SetGlobalVector("_MieG", new Vector4(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG, 1.0f / (4.0f * Mathf.PI)));
        cmd.SetGlobalVector("_VolumetricLight", new Vector2(scatteringCoef, extinctionCoef));

        cmd.SetGlobalVector("_LightPos", transform.position);
        cmd.SetGlobalVector("_LightDir", transform.forward);

        return Light.type switch
        {
            LightType.Point => SetupPointLight(cmd),
            LightType.Spot => SetupSpotLight(cmd),
            LightType.Directional => SetupDirectionalLight(cmd),
            _ => -1,
        };
    }
}
