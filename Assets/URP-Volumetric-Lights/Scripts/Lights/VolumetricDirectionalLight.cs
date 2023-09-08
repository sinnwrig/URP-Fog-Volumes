using UnityEngine;
using UnityEngine.Rendering;


public partial class VolumetricLight
{
    private void SetupDirectionalLight(VolumetricLightPass pass)
    {
        pass.DrawTestMesh(VolumetricLightPass.pointLightMesh, transform.localToWorldMatrix);
    }
}
