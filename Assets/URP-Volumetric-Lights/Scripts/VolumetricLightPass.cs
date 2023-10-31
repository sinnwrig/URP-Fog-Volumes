using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using System.Collections.Generic;
using System;
using Unity.Collections;
using System.Reflection;

public partial class VolumetricLightPass : ScriptableRenderPass
{
    private static readonly Plane[] frustumPlanes = new Plane[6];


    public struct SortedLight
    {
        public VisibleLight visibleLight;
        public int lightIndex;
        public VolumetricLight volumeLight;
    }


    public VolumetricLightFeature feature;
    public VolumetricResolution Resolution => feature.resolution;


    private static Material bilateralBlur;
    private static Material lightMaterial;


    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;

    public CommandBuffer commandBuffer;
    


    public VolumetricLightPass(VolumetricLightFeature feature, Shader blur, Shader light)
    {
        this.feature = feature;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (lightMaterial == null || lightMaterial.shader != light)
            lightMaterial = new Material(light);

        if (noiseTexture == null)
            noiseTexture = Resources.Load("Noise3DTexture") as Texture3D;

        if (ditherTexture == null)
            ditherTexture = Resources.Load("DitherTex") as Texture2D;
    }   



    private bool LightIsVisible(ref VisibleLight visibleLight, Plane[] frustumPlanes, Camera camera) 
	{
        if (visibleLight.lightType == LightType.Directional)
            return true;

        Light light = visibleLight.light;
        Vector3 position = light.transform.position;

		// Cull spherical range, ignoring camera far plane at index 5
		for (int i = 0; i < frustumPlanes.Length; i++) 
		{
			float distance = frustumPlanes[i].GetDistanceToPoint(position);

			if (distance < 0 && Mathf.Abs(distance) > light.range) 
				return false;
		}

        float distanceToSphereBounds = (camera.transform.position - position).magnitude - light.range;

        // Cull faraway lights
        if (distanceToSphereBounds > feature.lightRange)
            return false;

		return true;
	}


    private List<SortedLight> GetSortedLights(ref RenderingData renderingData)
    {
        LightData lightData = renderingData.lightData;
        NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

        List<SortedLight> sortedLights = new();

        GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera, frustumPlanes);
        
        for (int i = 0; i < visibleLights.Length; i++)
        {
            var visibleLight = visibleLights[i];

            if (!LightIsVisible(ref visibleLight, frustumPlanes, renderingData.cameraData.camera))
                continue;
            
            if (!visibleLight.light.TryGetComponent(out VolumetricLight volumeLight))
                continue;

            sortedLights.Add(new SortedLight
            {
                visibleLight = visibleLight,
                lightIndex = i,
                volumeLight = volumeLight,
            });
        }

        return sortedLights;
    }



    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

        commandBuffer = CommandBufferPool.Get("Volumetric Light Pass");


        var lights = GetSortedLights(ref renderingData);

        if (lights.Count == 0)
            return;

        DownsampleDepthBuffer();
        DrawLights(ref renderingData.lightData, lights);
        BilateralBlur(descriptor.width, descriptor.height);

        commandBuffer.SetGlobalTexture("_SourceTexture", source);
        commandBuffer.SetGlobalTexture("_SourceAdd", volumeLightTexture);

        // Use blit add kernel to merge source color and the blurred light texture
        commandBuffer.Blit(volumeLightTexture, source, lightMaterial, 3);

        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void SetShaderProperties()
    {
        // Set global shader variables
        commandBuffer.SetGlobalVector("_LightRange", new Vector2(feature.lightRange, feature.falloffRange));

        commandBuffer.SetGlobalTexture("_DitherTexture", ditherTexture);
        commandBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);

        commandBuffer.SetGlobalVector("_NoiseVelocity", feature.noiseVelocity * feature.noiseScale);
        commandBuffer.SetGlobalVector("_NoiseData", new Vector4(feature.noiseScale, feature.noiseIntensity, feature.noiseIntensityOffset));
    }


    private void DrawLights(ref LightData lightData, List<SortedLight> lights)
    {
        commandBuffer.GetTemporaryRT(tempId, lightBufferDescriptor, FilterMode.Point);

        var destinationA = VolumeLightBuffer;
        var destinationB = tempHandle;
        var latestDest = tempHandle;

        // Clear textures
        ClearColor(commandBuffer, destinationA, Color.black);
        ClearColor(commandBuffer, destinationB, Color.black);

        SetShaderProperties();

        // Light loop
        for (int i = 0; i < lights.Count; i++)
        {
            int pass = lights[i].volumeLight.SetShaderProperties(commandBuffer);

            if (pass < 0)
                continue;

            var source = latestDest;
            var target = source == destinationA ? destinationB : destinationA;

            commandBuffer.SetGlobalTexture("_SourceTexture", source);
            
            int lightIndex = lights[i].lightIndex;
            lightIndex = lightIndex == lightData.mainLightIndex ? -1 : lightIndex;

            commandBuffer.SetGlobalInt("_LightIndex", lightIndex);

            commandBuffer.Blit(source, target, lightMaterial, pass);
            latestDest = target;
        }

        if (latestDest == tempHandle)
            commandBuffer.Blit(latestDest, VolumeLightBuffer);
        
        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private void ClearColor(CommandBuffer cmd, RenderTargetIdentifier rt, Color color)
    {
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(RTClearFlags.Color, color, 1.0f, 0);
    }
}
