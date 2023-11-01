using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private int SetupSpotLight(CommandBuffer cmd)
    {
        float angle = Light.spotAngle / 2;
        float range = Light.range;

        float height = range * Mathf.Tan(angle * Mathf.Deg2Rad) * 2;

        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(height, height, range)).inverse;
        cmd.SetGlobalMatrix("_InvLightMatrix", matrix);

        // Use pass 0 - spot
        return 0;
    }
}
