using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupPointLight(VolumetricLightPass pass)
    {
        
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
