using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using System.Collections.Generic;
using Unity.Collections;
using System.Reflection;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    private static readonly FieldInfo shadowPassField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);
    
    private static bool GetShadowCasterPass(ref RenderingData renderingData, out AdditionalLightsShadowCasterPass pass)
    {
        pass = null;

        object obj = shadowPassField.GetValue(renderingData.cameraData.renderer);

        if (obj == null || obj is not AdditionalLightsShadowCasterPass castPass)
            return false;
        
        pass = castPass;
        return true;
    }

    private static readonly Plane[] cullingPlanes = new Plane[6];

    private static readonly HashSet<FogVolume> activeVolumes = new();


    public static void AddVolume(FogVolume volume) => activeVolumes.Add(volume);
    public static void RemoveVolume(FogVolume volume) => activeVolumes.Remove(volume);



    // Perform frustum culling and distance culling on light
    private bool LightIsVisible(ref VisibleLight visibleLight) 
	{
        if (visibleLight.lightType == LightType.Directional)
            return true;

        Vector3 position = visibleLight.localToWorldMatrix.GetColumn(3);

        for (int i = 0; i < cullingPlanes.Length; i++) 
		{
			float distance = cullingPlanes[i].GetDistanceToPoint(position);

			if (distance < 0 && Mathf.Abs(distance) > visibleLight.range) 
				return false;
		}

		return true;
	}


    // Setup all light constants to send to shader
    private List<NativeLight> SetupLights(ref RenderingData renderingData)
    {
        GetShadowCasterPass(ref renderingData, out AdditionalLightsShadowCasterPass shadowPass);

        LightData lightData = renderingData.lightData;
        NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

        List<NativeLight> initializedLights = new();

        for (int i = 0; i < visibleLights.Length; i++)
        {
            var visibleLight = visibleLights[i];

            if (!LightIsVisible(ref visibleLight))
                continue;

            // We should not need to access a private field to get shadow index, but we must. 
            int shadowIndex = shadowPass.GetShadowLightIndexFromLightIndex(i);

            NativeLight light = new()
            {
                shadowIndex = i == lightData.mainLightIndex ? -1 : shadowIndex, // Main light gets special treatment.
                range = visibleLight.range,
                screenRect = visibleLight.screenRect,
            };

            UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, i, 
                out light.position, 
                out light.color, 
                out light.attenuation, 
                out light.spotDirection, 
                out _
            );

            initializedLights.Add(light);
        }

        return initializedLights;
    }


    private List<FogVolume> SetupVolumes(ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        GeometryUtility.CalculateFrustumPlanes(camera, cullingPlanes);
        Vector3 camPos = camera.transform.position;

        List<FogVolume> fogVolumes = new();

        foreach (FogVolume volume in activeVolumes)
        {
            Bounds aabb = volume.GetAABB();

            // Volume is past maximum distance
            if ((camPos - aabb.ClosestPoint(camPos)).sqrMagnitude > volume.maxDistance * volume.maxDistance)
                continue;

            // Volume is outside camera frustum
            if (!GeometryUtility.TestPlanesAABB(cullingPlanes, aabb))
                continue;
                
            fogVolumes.Add(volume);
        }
        
        return fogVolumes;
    }

    
    private void DrawVolumes(List<FogVolume> volumes, ref RenderingData renderingData)
    {
        List<NativeLight> lights = SetupLights(ref renderingData);

        int perObjectLightCount = renderingData.lightData.maxPerObjectAdditionalLightsCount;

        commandBuffer.SetRenderTarget(VolumeFogBuffer);
        commandBuffer.ClearRenderTarget(true, true, Color.black);

        // Where the magic loop happens
        for (int i = 0; i < volumes.Count; i++)
        {
            volumes[i].RenderVolume(ref renderingData, commandBuffer, fogMaterial, lights, perObjectLightCount);
        }
    }
}
