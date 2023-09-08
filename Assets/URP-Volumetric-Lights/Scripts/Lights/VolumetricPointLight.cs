using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupPointLight(VolumetricLightPass pass)
    {
        float scale = _light.range * 2.0f;
        Matrix4x4 world = Matrix4x4.TRS(transform.position, _light.transform.rotation, new Vector3(scale, scale, scale));
        Mesh mesh = VolumetricLightPass.pointLightMesh;

        pass.DrawTestMesh(mesh, world);
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
