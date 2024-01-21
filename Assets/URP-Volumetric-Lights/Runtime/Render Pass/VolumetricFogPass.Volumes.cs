using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

using Unity.Collections;

using System.Reflection;
using System.Collections.Generic;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    // Why doesn't Unity expose this field?
    private static readonly FieldInfo shadowPassField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);

    // Try to extract the private shadow caster field with reflection
    private static bool GetShadowCasterPass(ref RenderingData renderingData, out AdditionalLightsShadowCasterPass pass)
    {
        pass = shadowPassField.GetValue(renderingData.cameraData.renderer) as AdditionalLightsShadowCasterPass;
        return pass == null;
    }


    // Instance of camera culling planes to avoid allocations
    private static readonly Plane[] cullingPlanes = new Plane[6];

    // Global set of all active volumes
    private static readonly HashSet<FogVolume> activeVolumes = new();

    public static void AddVolume(FogVolume volume) => activeVolumes.Add(volume);
    public static void RemoveVolume(FogVolume volume) => activeVolumes.Remove(volume);


    // Perform frustum culling on light
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

            // We should not need to access a private field to get shadow index 
            int shadowIndex = shadowPass.GetShadowLightIndexFromLightIndex(i);

            NativeLight light = new()
            {
                isDirectional = visibleLight.lightType == LightType.Directional,
                shadowIndex = i == lightData.mainLightIndex ? -1 : shadowIndex, // Main light gets special treatment
                range = visibleLight.range,
            };

            // Set up light properties
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


    // Collect all the visible active fog volumes
    private List<FogVolume> SetupVolumes(ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        GeometryUtility.CalculateFrustumPlanes(camera, cullingPlanes);
        Vector3 camPos = camera.transform.position;

        List<FogVolume> fogVolumes = new();

        foreach (FogVolume volume in activeVolumes)
        {
            if (volume.CullVolume(camPos, cullingPlanes))
                continue;
                
            fogVolumes.Add(volume);
        }
        
        return fogVolumes;
    }

    
    // Draw all of the volumes into our fog texture
    private void DrawVolumes(List<FogVolume> volumes, ref RenderingData renderingData)
    {
        List<NativeLight> lights = SetupLights(ref renderingData);

        int perObjectLightCount = renderingData.lightData.maxPerObjectAdditionalLightsCount;


        // Where the magic loop happens
        for (int i = 0; i < volumes.Count; i++)
        {
            volumes[i].DrawVolume(ref renderingData, commandBuffer, fogMaterial, lights, perObjectLightCount);
        }
    }
}
