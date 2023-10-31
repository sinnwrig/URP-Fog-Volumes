using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    [Tooltip("Max directional light marching distance")]
    public float maxRayLength = 400.0f;  


    private int SetupDirectionalLight(CommandBuffer cmd)
    {
        cmd.SetGlobalFloat("_MaxRayLength", maxRayLength);

        // use pass 2 - directional
        return 2;
    }
}
