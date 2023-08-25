using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupDirectionalLight(VolumetricLightPass pass)
    {
        int shaderPass = 4;

        _material.SetPass(shaderPass);

        _material.SetVector("_LightDir", new Vector4(_light.transform.forward.x, _light.transform.forward.y, _light.transform.forward.z, 1.0f / (_light.range * _light.range)));
        _material.SetVector("_LightColor", _light.color * _light.intensity);
        _material.SetFloat("_MaxRayLength", MaxRayLength);

        _material.EnableKeyword("DIRECTIONAL");

        _material.DisableKeyword("SHADOWS_DEPTH");
        pass.LightPassBuffer.Blit(null, pass.VolumeLightBuffer.identifier, _material, shaderPass);
    }
}
