using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private int SetupDirectionalLight(CommandBuffer cmd)
    {
        Matrix4x4 matrix = transform.worldToLocalMatrix;
        cmd.SetGlobalMatrix("_InvLightMatrix", matrix);

        // use pass 2 - directional
        return 2;
    }
}
