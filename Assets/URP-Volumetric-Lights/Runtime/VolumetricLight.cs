using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[RequireComponent(typeof(Light)), ExecuteAlways]
public partial class VolumetricLight : MonoBehaviour 
{
    private Light _light;
    public Light Light
    {
        get 
        {
            if (_light == null)
                _light = GetComponent<Light>();

            return _light;
        }
    }


    private MaterialPropertyBlock _block;
    public MaterialPropertyBlock Block
    {
        get
        {
            if (_block == null)
                _block = new MaterialPropertyBlock();
            
            return _block;
        }
    }


    [Range(1, 64)] public int sampleCount = 16;
    [Range(0.0f, 1.0f)] public float scatteringCoef = 0.5f;
    [Range(0.0f, 1f)] public float extinctionCoef = 0.01f;
    [Range(0.0f, 0.999f)] public float mieG = 0.1f;  
    public float maxRayLength = 25.0f;  


    public void RenderLight(CommandBuffer cmd, Material material, SortedLight light, float intensityModifier)
    {
        if (Light == null)
        {
            DestroyImmediate(this);
            return;
        }

        if (!Light.enabled)
            return;

        if (intensityModifier <= 0)
            return;

        Block.SetInt("_SampleCount", sampleCount);

        Block.SetFloat("_MieG", mieG);
        Block.SetVector("_VolumetricLight", new Vector2(scatteringCoef, extinctionCoef));
        Block.SetFloat("_MaxRayLength", maxRayLength);

        // Attenuation sampling params
        Block.SetInt("_LightIndex", light.index);
        Block.SetVector("_LightPosition", light.position);
        Block.SetVector("_LightColor", light.color * intensityModifier);
        Block.SetVector("_LightAttenuation", light.attenuation);
        Block.SetVector("_SpotDirection", light.spotDirection);

        Rect lightRect = light.visibleLight.screenRect;
        Vector4 rectVector = new Vector4(lightRect.x, lightRect.y, lightRect.width, lightRect.height);

        Block.SetVector("_ViewportRect", rectVector);

        switch (Light.type)
        {
            case LightType.Spot:
                RenderSpotLight(cmd, material);
            break;

            case LightType.Point:
                RenderPointLight(cmd, material);
            break;

            case LightType.Directional:
                RenderDirectionalLight(cmd, material);
            break;
        }
    }


    private void RenderSpotLight(CommandBuffer cmd, Material material)
    {
        float angle = Light.spotAngle / 2;
        float range = Light.range;

        float height = range * Mathf.Tan(angle * Mathf.Deg2Rad) * 2;

        Matrix4x4 spotMatrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(height, height, range));

        cmd.DrawMesh(MeshUtility.FullscreenMesh, spotMatrix, material, 0, 0, Block);
    }


    private void RenderPointLight(CommandBuffer cmd, Material material)
    {
        Matrix4x4 pointMatrix = Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one * Light.range * 2);

        cmd.DrawMesh(MeshUtility.FullscreenMesh, pointMatrix, material, 0, 1, Block);
    }


    private void RenderDirectionalLight(CommandBuffer cmd, Material material)
    {
        cmd.DrawMesh(MeshUtility.FullscreenMesh, Matrix4x4.identity, material, 0, 2, Block);
    }
}
