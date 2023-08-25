using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupPointLight(VolumetricLightPass pass)
    {
        int shaderPass = 0;

        if (!IsCameraInPointLightBounds())
        {
            shaderPass = 2;
        }

        _material.SetPass(shaderPass);

        _material.SetVector("_LightPos", new Vector4(_light.transform.position.x, _light.transform.position.y, _light.transform.position.z, 1.0f / (_light.range * _light.range)));
        _material.SetColor("_LightColor", _light.color * _light.intensity);

        _material.DisableKeyword("SHADOWS_CUBE");

        //pass.LightPassBuffer.DrawMesh(mesh, world, _material, 0, 5);///shaderPass);
    }


    private bool IsCameraInPointLightBounds()
    {
        float distanceSqr = (_light.transform.position - Camera.current.transform.position).sqrMagnitude;
        float extendedRange = _light.range + 1;

        if (distanceSqr < (extendedRange * extendedRange))
        {
            return true;
        }
        
        return false;
    }
}
