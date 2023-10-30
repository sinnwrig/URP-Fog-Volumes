using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private int SetupPointLight(CommandBuffer cmd)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one * Light.range * 2).inverse;
        cmd.SetGlobalMatrix("_InvLightMatrix", matrix);

        // Use pass 1 - Point
        return 1;
    }
}
