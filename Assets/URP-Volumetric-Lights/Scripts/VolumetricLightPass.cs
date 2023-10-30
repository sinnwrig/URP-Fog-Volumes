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
    private static readonly FieldInfo shadowCasterField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Plane[] frustumPlanes = new Plane[6];


    public struct SortedLight
    {
        public VisibleLight visibleLight;
        public int lightIndex;
        public int shadowIndex;
        public Light light => visibleLight.light;
    }


    public VolumetricResolution resolution;
    public VolumetricLightFeature feature;

    public float lightRange, falloffRange;


    private static Material bilateralBlur;
    private static Material volumetricLight;


    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;

    public CommandBuffer commandBuffer;
    


    public VolumetricLightPass(Shader blur, Shader light)
    {
        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (volumetricLight == null || volumetricLight.shader != light)
            volumetricLight = new Material(light);

        ValidateResources();
    }   


    private void ValidateResources()
    {
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
        if (distanceToSphereBounds > lightRange)
            return false;

		return true;
	}


    private List<SortedLight> GetSortedLights(ref RenderingData renderingData)
    {
        AdditionalLightsShadowCasterPass shadowCasterPass = (AdditionalLightsShadowCasterPass)shadowCasterField.GetValue(renderingData.cameraData.renderer);

        ref LightData lightData = ref renderingData.lightData;
        ref NativeArray<VisibleLight> visibleLights = ref lightData.visibleLights;

        List<SortedLight> sortedLights = new();


        GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera, frustumPlanes);

        
        for (int i = 0; i < visibleLights.Length; i++)
        {
            var visibleLight = visibleLights[i];

            if (LightIsVisible(ref visibleLight, frustumPlanes, renderingData.cameraData.camera))
            {   
                sortedLights.Add(new SortedLight
                {
                    visibleLight = visibleLight,
                    lightIndex = i,
                    shadowIndex = shadowCasterPass.GetShadowLightIndexFromLightIndex(i)
                });
            }
        }

        return sortedLights;
    }



    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        ref var lightData = ref renderingData.lightData;

        var lights = GetSortedLights(ref renderingData);

        if (lights.Count == 0)
        {
            return;
        }

        commandBuffer = CommandBufferPool.Get("Volumetric Light Pass");

        DownsampleDepthBuffer();

        DrawLights(ref lightData, lights);

        // Blur and upsample volumetric light texture
        BilateralBlur(descriptor.width, descriptor.height);

        commandBuffer.SetGlobalTexture("_SourceTexture", source);
        commandBuffer.SetGlobalTexture("_SourceAdd", volumeLightTexture);

        // Use blit add kernel to merge source color and the blurred light texture
        commandBuffer.Blit(volumeLightTexture, source, volumetricLight, 3);

        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void DrawLights(ref LightData lightData, List<SortedLight> lights)
    {
        commandBuffer.GetTemporaryRT(tempId, lightBufferDescriptor, FilterMode.Point);

        var destinationA = VolumeLightBuffer;
        var destinationB = tempHandle;
        var latestDest = tempHandle;

        // Clear destination texture
        commandBuffer.SetRenderTarget(latestDest);
        commandBuffer.ClearRenderTarget(false, true, Color.black);

        // Set global shader variables
        commandBuffer.SetGlobalVector("_LightRange", new Vector2(lightRange, falloffRange));
        commandBuffer.SetGlobalTexture("_DitherTexture", ditherTexture);
        commandBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);

        // Light loop
        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i].light.TryGetComponent(out VolumetricLight lightComponent))
            {
                int lightIndex = lights[i].lightIndex;
                commandBuffer.SetGlobalInt("_LightIndex", lightIndex == lightData.mainLightIndex ? -1 : lightIndex);

                int pass = lightComponent.SetShaderProperties(commandBuffer);

                if (pass < 0)
                    continue;

                var source = latestDest;
                var target = source == destinationA ? destinationB : destinationA;

                commandBuffer.SetGlobalTexture("_SourceTexture", source);
            
                commandBuffer.Blit(source, target, volumetricLight, pass);
                latestDest = target;
            }
        }

        if (latestDest == tempHandle)
            commandBuffer.Blit(latestDest, VolumeLightBuffer);
        
        commandBuffer.ReleaseTemporaryRT(tempId);
    }
}
