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
        public VolumetricLight volumeLight;
        public Vector4 position;
        public Vector4 color;
        public Vector4 attenuation;
        public Vector4 spotDirection;
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
        var shadowCasterPass = (AdditionalLightsShadowCasterPass)shadowCasterField.GetValue(renderingData.cameraData.renderer);

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

            SortedLight light = new()
            {
                visibleLight = visibleLight,
                lightIndex = i == lightData.mainLightIndex ? -1 : i,
                shadowIndex = shadowCasterPass.GetShadowLightIndexFromLightIndex(i),
                volumeLight = volumeLight,
            };

            UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, i, out light.position, out light.color, out light.attenuation, out light.spotDirection, out _);

            sortedLights.Add(light);
        }

        return sortedLights;
    }



    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var renderer = renderingData.cameraData.renderer;

        #if UNITY_2022_1_OR_NEWER
            var cameraColor = renderer.cameraColorTargetHandle;
            var cameraDepth = renderer.cameraDepthTargetHandle;
        #else
            var cameraColor = renderer.cameraColorTarget;
            var cameraDepth = renderer.cameraDepthTarget;
        #endif

        var source = cameraColor;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

        commandBuffer = CommandBufferPool.Get("Volumetric Light Pass");

        var lights = GetSortedLights(ref renderingData);

        if (lights.Count == 0)
            return;

        DownsampleDepthBuffer();
        DrawLights(lights);
        BilateralBlur(descriptor.width, descriptor.height);
        BlendLights(cameraColor, descriptor);

        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void SetShaderProperties()
    {
        // Set global shader variables
        commandBuffer.SetGlobalVector("_LightRange", new Vector2(feature.lightRange - feature.falloffRange, feature.lightRange));

        commandBuffer.SetGlobalTexture("_DitherTexture", ditherTexture);
        commandBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);

        commandBuffer.SetGlobalVector("_NoiseVelocity", feature.noiseVelocity * feature.noiseScale);
        commandBuffer.SetGlobalVector("_NoiseData", new Vector4(feature.noiseScale, feature.noiseIntensity, feature.noiseIntensityOffset));
    }


    private void DrawLights(List<SortedLight> lights)
    {
        commandBuffer.GetTemporaryRT(tempId, lightBufferDescriptor, FilterMode.Point);

        var source = VolumeLightBuffer;
        var target = tempHandle;

        // Clear initial texture
        ClearColor(commandBuffer, target, Color.black);

        SetShaderProperties();

        // Light loop
        for (int i = 0; i < lights.Count; i++)
        {
            (source, target) = (target, source);
            DrawLight(lights[i], source, target);
        }

        if (target == tempHandle)
            commandBuffer.Blit(target, VolumeLightBuffer);
        
        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private void DrawLight(SortedLight light, RenderTargetIdentifier source, RenderTargetIdentifier target)
    {
        int pass = light.volumeLight.SetShaderProperties(commandBuffer);

        if (pass < 0)
            return;
    
        commandBuffer.SetGlobalInt("_LightIndex", light.lightIndex);
        commandBuffer.SetGlobalInt("_ShadowIndex", light.shadowIndex);
        commandBuffer.SetGlobalTexture("_SourceTexture", source);
        commandBuffer.SetGlobalVector("_LightPosition", light.position);
        commandBuffer.SetGlobalVector("_LightColor", light.color);
        commandBuffer.SetGlobalVector("_LightAttenuation", light.attenuation);
        commandBuffer.SetGlobalVector("_SpotDirection", light.spotDirection);

        commandBuffer.Blit(source, target, lightMaterial, pass);
    }


    private void BlendLights(RenderTargetIdentifier target, RenderTextureDescriptor targetDescriptor)
    {
        commandBuffer.GetTemporaryRT(tempId, targetDescriptor);
        commandBuffer.Blit(target, tempHandle);

        commandBuffer.SetGlobalTexture("_SourceTexture", tempHandle);
        commandBuffer.SetGlobalTexture("_SourceAdd", volumeLightTexture);

        // Use blit add kernel to merge target color and the light buffer
        commandBuffer.Blit(volumeLightTexture, target, lightMaterial, 3);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private void ClearColor(CommandBuffer cmd, RenderTargetIdentifier rt, Color color)
    {
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(RTClearFlags.Color, color, 1.0f, 0);
    }
}
