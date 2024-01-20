using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

using Unity.Collections;

using System.Reflection;
using System.Collections.Generic;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    public enum VolumetricResolution
    {
        Full,
        Half,
        Quarter
    }


    // Why doesn't Unity expose this field?
    private static readonly FieldInfo shadowPassField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);

    // Try to extract the private shadow caster field with reflection
    private static bool GetShadowCasterPass(ref RenderingData renderingData, out AdditionalLightsShadowCasterPass pass)
    {
        pass = shadowPassField.GetValue(renderingData.cameraData.renderer) as AdditionalLightsShadowCasterPass;
        return pass == null;
    }


    // Global keywords

    public static readonly GlobalKeyword noiseKeyword = GlobalKeyword.Create("NOISE_ENABLED");
    public static readonly GlobalKeyword lightingKeyword = GlobalKeyword.Create("LIGHTING_ENABLED");
    public static readonly GlobalKeyword shadowsKeyword = GlobalKeyword.Create("SHADOWS_ENABLED");


    // Blur keywords
    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");

    private static readonly GlobalKeyword fullDepthSource = GlobalKeyword.Create("SOURCE_FULL_DEPTH");


    // Render Targets

    // Depth render targets
    private static readonly int halfDepthId = Shader.PropertyToID("_HalfDepthTarget");
    private static readonly RenderTargetIdentifier halfDepthTarget = new(halfDepthId);
    private static readonly int quarterDepthId = Shader.PropertyToID("_QuarterDepthTarget");
    private static readonly RenderTargetIdentifier quarterDepthTarget = new(quarterDepthId);

    // Light render targets
    private static readonly int volumeFogId = Shader.PropertyToID("_VolumeFogTexture");
    private static readonly RenderTargetIdentifier volumeFogTexture = new(volumeFogId);
    private static readonly int halfVolumeFogId = Shader.PropertyToID("_HalfVolumeFogTexture");
    private static readonly RenderTargetIdentifier halfVolumeFogTexture = new(halfVolumeFogId);
    private static readonly int quarterVolumeFogId = Shader.PropertyToID("_QuarterVolumeFogTexture");
    private static readonly RenderTargetIdentifier quarterVolumeFogTexture = new(quarterVolumeFogId);

    // Temporal Reprojection Target
    private static readonly int reprojectionTargetId = Shader.PropertyToID("_VolumeFogReprojection");
    private static readonly RenderTargetIdentifier reprojectionTexture = new(reprojectionTargetId);


    // Temp render target 
    private static readonly int tempId = Shader.PropertyToID("_Temp");
    private RenderTargetIdentifier tempHandle = new(tempId);


    private static Material bilateralBlur;
    private static Material fogMaterial;


    // Instance of camera culling planes to avoid allocations
    private static readonly Plane[] cullingPlanes = new Plane[6];

    // Global set of all active volumes
    private static readonly HashSet<FogVolume> activeVolumes = new();

    public static void AddVolume(FogVolume volume) => activeVolumes.Add(volume);
    public static void RemoveVolume(FogVolume volume) => activeVolumes.Remove(volume);


    private VolumetricFogFeature feature;
    private VolumetricResolution Resolution => feature.resolution;

    private CommandBuffer commandBuffer;


    // Active resolution target
    private RenderTargetIdentifier VolumeFogBuffer 
    {
        get 
        {
            return Resolution switch
            {
                VolumetricResolution.Quarter => quarterVolumeFogTexture,
                VolumetricResolution.Half => halfVolumeFogTexture,
                VolumetricResolution.Full => volumeFogTexture,
                _ => volumeFogTexture,
            };
        }
    }
    


    public VolumetricFogPass(VolumetricFogFeature feature, Shader blur, Shader fog)
    {
        this.feature = feature;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (fogMaterial == null || fogMaterial.shader != fog)
            fogMaterial = new Material(fog);
    }   


    // Allocate temporary textures
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
    {
        RenderTextureDescriptor descriptor = data.cameraData.cameraTargetDescriptor;

        int width = descriptor.width;
        int height = descriptor.height;
        var colorFormat = RenderTextureFormat.ARGBHalf;
        var depthFormat = RenderTextureFormat.RFloat;

        cmd.GetTemporaryRT(volumeFogId, width, height, 0, FilterMode.Point, colorFormat);

        if (Resolution == VolumetricResolution.Half)
            cmd.GetTemporaryRT(halfVolumeFogId, width / 2, height / 2, 0, FilterMode.Bilinear, colorFormat);

        // Half/Quarter res both need half-res depth buffer for downsampling
        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
            cmd.GetTemporaryRT(halfDepthId, width / 2, height / 2, 0, FilterMode.Point, depthFormat);

        if (Resolution == VolumetricResolution.Quarter)
        {
            cmd.GetTemporaryRT(quarterVolumeFogId, width / 4, height / 4, 0, FilterMode.Bilinear, colorFormat);
            cmd.GetTemporaryRT(quarterDepthId, width / 4, height / 4, 0, FilterMode.Point, depthFormat);
        }
    }


    private void SetupReprojectionTexture(CommandBuffer cmd, ref RenderingData data)
    {
        
    }


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

        commandBuffer.SetRenderTarget(VolumeFogBuffer);
        commandBuffer.ClearRenderTarget(true, true, Color.black);

        // Where the magic loop happens
        for (int i = 0; i < volumes.Count; i++)
        {
            volumes[i].DrawVolume(ref renderingData, commandBuffer, fogMaterial, lights, perObjectLightCount);
        }
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.postProcessEnabled)
            return;

        var fogVolumes = SetupVolumes(ref renderingData);
        if (fogVolumes.Count == 0)
            return;

        var renderer = renderingData.cameraData.renderer;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

    #if UNITY_2022_1_OR_NEWER
        var cameraColor = renderer.cameraColorTargetHandle;
        var cameraDepth = renderer.cameraDepthTargetHandle;
    #else
        var cameraColor = renderer.cameraColorTarget;
        var cameraDepth = renderer.cameraDepthTarget;
    #endif

        commandBuffer = CommandBufferPool.Get("Volumetric Fog Pass");

        DownsampleDepthBuffer();

        DrawVolumes(fogVolumes, ref renderingData);

        BilateralBlur(descriptor.width, descriptor.height);
        BlendFog(cameraColor);

        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    // Release temporary textures
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(volumeFogId);

        if (Resolution == VolumetricResolution.Half)
            cmd.ReleaseTemporaryRT(halfVolumeFogId);

        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
            cmd.ReleaseTemporaryRT(halfDepthId);

        if (Resolution == VolumetricResolution.Quarter)
        {
            cmd.ReleaseTemporaryRT(quarterVolumeFogId);
            cmd.ReleaseTemporaryRT(quarterDepthId);
        }
    }


    // Additive blend the fog volume with the scene
    private void BlendFog(RTHandle target)
    {
        commandBuffer.GetTemporaryRT(tempId, target.rt.descriptor);
        commandBuffer.Blit(target, tempHandle);

        commandBuffer.SetGlobalTexture("_BlitSource", tempHandle);
        commandBuffer.SetGlobalTexture("_BlitAdd", volumeFogTexture);

        // Use blit add kernel to merge target color and the light buffer
        commandBuffer.Blit(null, target, fogMaterial, 0);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    // Blurs the active resolution texture, then upscales and blits (if neccesary) to full resolution texture
    private void BilateralBlur(int width, int height)
    {
        commandBuffer.DisableKeyword(fullResKernel);
        commandBuffer.DisableKeyword(halfResKernel);
        commandBuffer.DisableKeyword(quarterResKernel);

        // Blur quarter-res texture and upsample to full res
        if (Resolution == VolumetricResolution.Quarter)
        {
            commandBuffer.EnableKeyword(quarterResKernel);
            BilateralBlur(quarterVolumeFogTexture, quarterDepthTarget, width / 4, height / 4); 
            
            // Upsample to full res
            Upsample(quarterVolumeFogTexture, quarterDepthTarget, volumeFogTexture);
            return;
        }
        
        // Blur half-res texture and upsample to full res
        if (Resolution == VolumetricResolution.Half)
        {
            commandBuffer.EnableKeyword(halfResKernel);
            BilateralBlur(halfVolumeFogTexture, halfDepthTarget, width / 2, height / 2);

            // Upsample to full res
            Upsample(halfVolumeFogTexture, halfDepthTarget, volumeFogTexture);
            return;
        }

        // Blur full-scale texture- use full-scale depth texture from shader
        commandBuffer.EnableKeyword(fullResKernel);
        BilateralBlur(volumeFogTexture, null, width, height);
    }


    // Blurs source texture with provided depth texture to preserve edges- uses camera depth texture if none is provided
    private void BilateralBlur(RenderTargetIdentifier source, RenderTargetIdentifier? depthBuffer, int sourceWidth, int sourceHeight)
    {
        commandBuffer.GetTemporaryRT(tempId, sourceWidth, sourceHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

        SetDepthTexture("_DepthTexture", depthBuffer);

        // Horizontal blur
        commandBuffer.SetGlobalTexture("_BlurSource", source);
        commandBuffer.Blit(null, tempHandle, bilateralBlur, 0);

        // Vertical blur
        commandBuffer.SetGlobalTexture("_BlurSource", tempHandle);
        commandBuffer.Blit(null, source, bilateralBlur, 1);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    // Downsamples depth texture to active resolution buffer
    private void DownsampleDepthBuffer()
    {
        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
        {
            SetDepthTexture("_DownsampleSource", null);
            commandBuffer.Blit(null, halfDepthTarget, bilateralBlur, 2);
        }

        if (Resolution == VolumetricResolution.Quarter)
        {
            SetDepthTexture("_DownsampleSource", halfDepthTarget);
            commandBuffer.Blit(null, quarterDepthTarget, bilateralBlur, 2);
        }
    }


    // Perform depth-aware upsampling to the destination
    private void Upsample(RenderTargetIdentifier sourceColor, RenderTargetIdentifier sourceDepth, RenderTargetIdentifier destination)
    {
        commandBuffer.SetGlobalTexture("_DownsampleColor", sourceColor);
        commandBuffer.SetGlobalTexture("_DownsampleDepth", sourceDepth);

        commandBuffer.Blit(null, destination, bilateralBlur, 3);
    }


    // Use shader variants to either 
    // 1: Use the depth texture being assigned 
    // 2: Use the _CameraDepthTexture property
    private void SetDepthTexture(string textureId, RenderTargetIdentifier? depth)
    {
        commandBuffer.SetKeyword(fullDepthSource, !depth.HasValue);

        if (depth.HasValue)
            commandBuffer.SetGlobalTexture(textureId, depth.Value);
    }
}
