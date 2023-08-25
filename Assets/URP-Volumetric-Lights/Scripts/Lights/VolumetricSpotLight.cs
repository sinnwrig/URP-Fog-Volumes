using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupSpotLight(VolumetricLightPass pass)
    {
        int shaderPass = 1;

        if (!IsCameraInSpotLightBounds())
        {
            shaderPass = 3;     
        }

        _material.SetPass(shaderPass);
        _material.SetVector("_LightPos", new Vector4(_light.transform.position.x, _light.transform.position.y, _light.transform.position.z, 1.0f / (_light.range * _light.range)));
        _material.SetVector("_LightColor", _light.color * _light.intensity);


        Vector3 apex = transform.position;
        Vector3 axis = transform.forward;
        
        // plane equation ax + by + cz + d = 0; precompute d here to lighten the shader
        Vector3 center = apex + axis * _light.range;
        float d = -Vector3.Dot(center, axis);

        // update material
        _material.SetFloat("_PlaneD", d);        
        _material.SetFloat("_CosAngle", Mathf.Cos((_light.spotAngle + 1) * 0.5f * Mathf.Deg2Rad));

        _material.SetVector("_ConeApex", new Vector4(apex.x, apex.y, apex.z));
        _material.SetVector("_ConeAxis", new Vector4(axis.x, axis.y, axis.z));

        _material.EnableKeyword("SPOT");
        _material.DisableKeyword("SHADOWS_DEPTH");

        //pass.LightPassBuffer.DrawMesh(mesh, world, _material, 0, shaderPass);
    }


    private bool IsCameraInSpotLightBounds()
    {
        // check range
        float distance = Vector3.Dot(_light.transform.forward, Camera.current.transform.position - _light.transform.position);
        float extendedRange = _light.range + 1;
        if (distance > extendedRange)
        {
            return false;
        }

        // check angle
        float cosAngle = Vector3.Dot(transform.forward, (Camera.current.transform.position - _light.transform.position).normalized);
        if((Mathf.Acos(cosAngle) * Mathf.Rad2Deg) > (_light.spotAngle + 3) * 0.5f)
        {
            return false;
        }

        return true;
    }
}
