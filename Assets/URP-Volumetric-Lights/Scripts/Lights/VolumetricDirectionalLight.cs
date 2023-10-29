using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupDirectionalLight(Material volumetricMaterial, VolumetricLightPass pass)
    {
        // Pass 2 - Directional
        BlitUtility.BlitNext(volumetricMaterial, "_SourceTexture", 2);
    }
}
