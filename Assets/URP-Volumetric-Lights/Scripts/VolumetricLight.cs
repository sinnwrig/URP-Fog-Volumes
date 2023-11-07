using UnityEngine;


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


    [Range(1, 64)] public int sampleCount = 16;
    [Range(0.0f, 1.0f)] public float scatteringCoef = 0.5f;
    [Range(0.0f, 1f)] public float extinctionCoef = 0.01f;
    [Range(0.0f, 0.999f)] public float mieG = 0.1f;  
    public float maxRayLength = 400.0f;  


    public bool CanRender()
    {
        return Light != null && Light.enabled;
    }


    public Matrix4x4 GetLightMatrix()
    {
        return Light.type switch
        {
            LightType.Point => Matrix4x4.TRS(transform.position, Quaternion.identity, Vector3.one * Light.range * 2).inverse,
            LightType.Spot => SpotLightMatrix(),
            _ => Matrix4x4.identity,
        };
    }

    public Vector3 GetMie()
    {
        return new Vector3(1 - (mieG * mieG), 1 + (mieG * mieG), 2 * mieG);
    }


    private Matrix4x4 SpotLightMatrix()
    {
        float angle = Light.spotAngle / 2;
        float range = Light.range;

        float height = range * Mathf.Tan(angle * Mathf.Deg2Rad) * 2;

        return Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(height, height, range)).inverse;
    }
}
