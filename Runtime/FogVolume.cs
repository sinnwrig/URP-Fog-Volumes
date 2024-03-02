using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


[ExecuteAlways]
public partial class FogVolume : MonoBehaviour 
{
    public VolumeType volumeType;
    public Vector3 edgeFade = Vector3.zero;

    [Min(0)] 
    public float maxDistance = 100.0f;

    [Range(0, 0.999f)] 
    public float distanceFade = 1.0f;

    public LayerMask lightLayerMask = ~0;

    public bool disableLightLimit;


    private FogVolumeProfile _internalProfile;

    public FogVolumeProfile sharedProfile = null;

    public FogVolumeProfile profile
    {
        get
        {
            if (_internalProfile == null)
            {
                if (sharedProfile != null)
                    _internalProfile = Instantiate(sharedProfile);
            }

            return _internalProfile;
        }

        set => _internalProfile = value;
    }

    public bool HasInstantiatedProfile() => _internalProfile != null;

    public FogVolumeProfile profileReference => _internalProfile == null ? sharedProfile : _internalProfile;


    private MaterialPropertyBlock _propertyBlock;

    private MaterialPropertyBlock PropertyBlock
    {
        get
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            
            return _propertyBlock;
        }
    }


    private const int maxLightCount = 32;

    int lightCount;
    private static readonly float[] lightsToShadow = new float[maxLightCount];
    private static readonly Vector4[] lightPositions  = new Vector4[maxLightCount];
    private static readonly Vector4[] lightColors = new Vector4[maxLightCount];
    private static readonly Vector4[] lightAttenuations = new Vector4[maxLightCount];
    private static readonly Vector4[] spotDirections = new Vector4[maxLightCount];


    void OnEnable() => VolumetricFogPass.AddVolume(this);
    void OnDisable() => VolumetricFogPass.RemoveVolume(this);
    void OnDestroy() => OnDisable();


    private void SetupViewport(ref RenderingData renderingData)
    {
        Vector3[] boundsPoints = volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder ? BoundsUtility.capsuleCorners : BoundsUtility.cubeCorners;

        Vector4 viewport = BoundsUtility.GetViewportRect(transform.localToWorldMatrix, renderingData.cameraData.camera, boundsPoints);

        if (BoundsUtility.InsideBounds(transform.worldToLocalMatrix, renderingData.cameraData.camera.transform.position, GetBounds()))
            viewport = new Vector4(0, 0, 1, 1);

        PropertyBlock.SetVector("_ViewportRect", viewport);
    }


    private Vector3 EdgeFade()
    {
        switch (volumeType)
        {
            case VolumeType.Cube:
                float edgeX = -((1 - edgeFade.x) * 2 - 1) * 0.5f;
                float edgeY = -((1 - edgeFade.y) * 2 - 1) * 0.5f;
                float edgeZ = -((1 - edgeFade.z) * 2 - 1) * 0.5f;

                return new Vector3(edgeX, edgeY, edgeZ);

            case VolumeType.Cylinder:
                float cylX = -((1 - edgeFade.x) * 2 - 1) * 0.5f;
                float cylY = -(1 - (edgeFade.y * 2));
                return new Vector3(cylX, cylY, 0);

            default:
                float sphereX = -((1 - edgeFade.x) * 2 - 1) * 0.5f;
                return new Vector3(sphereX, 0, 0);
        }
    }


    // Upload the list of affecting lights to the shader
    private void SetupLighting(List<NativeLight> lights, int maxLights)
    {
        maxLights = disableLightLimit ? maxLightCount : Math.Min(maxLightCount, maxLights);

        PropertyBlock.SetVector("_EdgeFade", EdgeFade());

        if (profileReference.lightingMode != LightingMode.Unlit)
        {
            Bounds bounds = GetBounds();
            Matrix4x4 trs = transform.localToWorldMatrix;
            Matrix4x4 invTrs = transform.worldToLocalMatrix;

            lightCount = 0;
            for (int i = 0; i < Math.Min(lights.Count, maxLights); i++)
            {   
                NativeLight light = lights[i];

                if (lightLayerMask != (lightLayerMask | (1 << light.layer)))
                    continue;

                if (light.isDirectional || BoundsUtility.WithinDistance(trs, invTrs, light.position, light.range, bounds))
                {
                    lightsToShadow[lightCount] = light.shadowIndex;
                    lightPositions[lightCount] = light.position;
                    lightColors[lightCount] = light.color;
                    lightAttenuations[lightCount] = light.attenuation;
                    spotDirections[lightCount] = light.spotDirection;

                    lightCount++;
                }
            }

            PropertyBlock.SetInteger("_LightCount", lightCount);  

            // Initialize the shader light arrays
            PropertyBlock.SetFloatArray("_LightToShadowIndices", lightsToShadow);
            PropertyBlock.SetVectorArray("_LightPositions", lightPositions);   
            PropertyBlock.SetVectorArray("_LightColors", lightColors);        
            PropertyBlock.SetVectorArray("_LightAttenuations", lightAttenuations);  
            PropertyBlock.SetVectorArray("_SpotDirections", spotDirections); 
        }
    }


    public void DrawVolume(ref RenderingData renderingData, CommandBuffer cmd, Shader shader, List<NativeLight> lights, int maxLights)
    {
        PropertyBlock.SetVector("_FogRange", new Vector2(maxDistance, Mathf.Lerp(0, maxDistance, distanceFade)));

        Material material = profileReference.GetMaterial(shader, cmd);
        
        volumeType.SetVolumeKeyword(cmd);

        SetupViewport(ref renderingData);
        SetupLighting(lights, maxLights);

        PropertyBlock.SetMatrix("_InvMatrix", transform.worldToLocalMatrix);

        cmd.DrawMesh(MeshUtility.FullscreenMesh, Matrix4x4.identity, material, 0, 0, PropertyBlock);
    }
    

    // Whether or not the volume can be culled by the frustum or distance
    public bool CullVolume(Vector3 cameraPosition, Plane[] cameraPlanes)
    {
        if (profileReference == null)
            return true;

        Bounds aabb = GetAABB();

        // Volume is past maximum distance
        if ((cameraPosition - aabb.ClosestPoint(cameraPosition)).sqrMagnitude > maxDistance * maxDistance)
            return true;

        // Volume is outside camera frustum
        if (!GeometryUtility.TestPlanesAABB(cameraPlanes, aabb))
            return true;
        
        return false;
    }


    public Bounds GetBounds()
    {
        if (volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder) 
            return BoundsUtility.capsuleBounds;

        // Default to cube
        return BoundsUtility.cubeBounds;
    }


    public Bounds GetAABB()
    {
        if (volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder) 
            return GeometryUtility.CalculateBounds(BoundsUtility.capsuleCorners, transform.localToWorldMatrix);

        // Default to cube
        return GeometryUtility.CalculateBounds(BoundsUtility.cubeCorners, transform.localToWorldMatrix);
    }


    public void OnDrawGizmosSelected()
    {
        for (int i = 0; i < lightCount; i++)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lightPositions[i], 0.5f);
        }
    }
}
