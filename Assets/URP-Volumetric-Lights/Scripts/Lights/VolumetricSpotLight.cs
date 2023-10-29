using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupSpotLight(Material volumetricMaterial, VolumetricLightPass pass)
    {
        float angle = Light.spotAngle / 2;
        float range = Light.range;

        float height = range * Mathf.Tan(angle * Mathf.Deg2Rad) * 2;

        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(height, height, range)).inverse;
        BlitUtility.blitCommandBuffer.SetGlobalMatrix("_InvLightMatrix", matrix);

        // Pass 0 - Spot
        BlitUtility.BlitNext(volumetricMaterial, "_SourceTexture", 0);
    }
}
