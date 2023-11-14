using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[RequireComponent(typeof(Light)), ExecuteAlways]
public partial class VolumetricLight : MonoBehaviour 
{
    private static Mesh _fullscreenMesh;

    public static Mesh FullscreenMesh
    {
        get
        {
            if (_fullscreenMesh != null)
                return _fullscreenMesh;

            _fullscreenMesh = new Mesh
            {
                name = "Fullscreen Quad",
                vertices = new Vector3[]
                {
                    new(-1.0f, -1.0f, 0.0f),
                    new(-1.0f,  1.0f, 0.0f),
                    new(1.0f, -1.0f, 0.0f),
                    new(1.0f,  1.0f, 0.0f)
                },

                uv = new Vector2[]
                {
                    new(0.0f, 0.0f),
                    new(0.0f, 1.0f),
                    new(1.0f, 0.0f),
                    new(1.0f, 1.0f)
                },

                triangles = new int[] { 0, 1, 2, 2, 1, 3 }
            };

            _fullscreenMesh.UploadMeshData(true);
            return _fullscreenMesh;
        }
    }

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


    public bool CanRender()
    {
        return Light != null && Light.enabled;
    }


    public Matrix4x4 PointLightMatrix()
    {
        return Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one * Light.range * 2);
    }


    private Matrix4x4 SpotLightMatrix()
    {
        float angle = Light.spotAngle / 2;
        float range = Light.range;

        float height = range * Mathf.Tan(angle * Mathf.Deg2Rad) * 2;

        return Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(height, height, range));
    }


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

        Rect rect = light.visibleLight.screenRect;
        Block.SetVector("_ViewportRect", new Vector4(rect.x, rect.y, rect.width, rect.height));

        switch (Light.type)
        {
            case LightType.Spot:
                cmd.DrawMesh(FullscreenMesh, SpotLightMatrix(), material, 0, 0, Block);
            break;

            case LightType.Point:
                cmd.DrawMesh(FullscreenMesh, PointLightMatrix(), material, 0, 1, Block);
            break;

            case LightType.Directional:
                cmd.DrawMesh(FullscreenMesh, Matrix4x4.identity, material, 0, 2, Block);
            break;
        }
    }
}
