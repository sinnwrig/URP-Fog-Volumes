using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupPointLight(Material volumetricMaterial, VolumetricLightPass pass)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one * Light.range * 2).inverse;
        BlitUtility.blitCommandBuffer.SetGlobalMatrix("_InvLightMatrix", matrix);

        // Pass 1 - Point 
        BlitUtility.BlitNext(volumetricMaterial, "_SourceTexture", 1);
    }
}
