using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupSpotLight(VolumetricLightPass pass)
    {
        float scale = _light.range;
        float angleScale = Mathf.Tan((_light.spotAngle + 1) * 0.5f * Mathf.Deg2Rad) * _light.range;

        Matrix4x4 world = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(angleScale, angleScale, scale));
        Mesh mesh = VolumetricLightPass.spotLightMesh;

        pass.DrawTestMesh(mesh, world);
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
