using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupSpotLight(VolumetricLightPass pass)
    {
        
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
