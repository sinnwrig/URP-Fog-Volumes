using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

using Unity.Collections;

using System.Reflection;
using System.Collections.Generic;


public class VolumetricFogPass : ScriptableRenderPass
{
    public enum VolumetricResolution
    {
        Full,
        Half,
        Quarter
    }

    
    public static readonly GlobalKeyword reprojectionKeyword = GlobalKeyword.Create("TEMPORAL_REPROJECTION_ENABLED");

    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR");
    private static readonly GlobalKeyword fullDepthSource = GlobalKeyword.Create("SOURCE_FULL_DEPTH");


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

    // Temp render target 
    private static readonly int tempId = Shader.PropertyToID("_Temp");
    private RenderTargetIdentifier tempHandle = new(tempId);


    private static readonly FieldInfo shadowPassField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);

    // Try to extract the private shadow caster field with reflection
    private static bool GetShadowCasterPass(ref RenderingData renderingData, out AdditionalLightsShadowCasterPass pass)
    {
        pass = shadowPassField.GetValue(renderingData.cameraData.renderer) as AdditionalLightsShadowCasterPass;
        return pass != null;
    }


    private static readonly Plane[] cullingPlanes = new Plane[6];


    // Global set of active volumes
    private static readonly HashSet<FogVolume> activeVolumes = new();

    public static void AddVolume(FogVolume volume) => activeVolumes.Add(volume);
    public static void RemoveVolume(FogVolume volume) => activeVolumes.Remove(volume);


    // Global material references
    private static Material bilateralBlur;
    private static Shader fogShader;
    private static Material blitAdd;
    private static Material reprojection;


    private VolumetricFogFeature feature;
    private CommandBuffer commandBuffer;

    private VolumetricResolution Resolution
    {
        get
        {
            // Temporal reprojection forces full-res rendering
            if (feature.resolution != VolumetricResolution.Full && feature.enableReprojection)
                return VolumetricResolution.Full;
            
            return feature.resolution;
        }   
    }


    // Previous frame reprojection matrices
    private Matrix4x4 prevVMatrix;
    private Matrix4x4 prevVpMatrix;
    private Matrix4x4 prevInvVpMatrix;

    // Temporal pass iterator
    private int temporalPass;

    // Temporal Reprojection Target-
    // NOTE: only a RenderTexture seems to preserve information between frames on my device, otherwise I'd use an RTHandle or RenderTargetIdentifier
    private RenderTexture reprojectionBuffer;
    


    public VolumetricFogPass(VolumetricFogFeature feature, Shader blur, Shader fog, Shader add, Shader reproj)
    {
        this.feature = feature;

        fogShader = fog;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (blitAdd == null || blitAdd.shader != add)
            blitAdd = new Material(add);

        if (reprojection == null || reprojection.shader != reproj)
            reprojection = new Material(reproj);
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
            if (!volume.CullVolume(camPos, cullingPlanes))
                fogVolumes.Add(volume);
        }
        
        return fogVolumes;
    }

    
    // Draw all of the volumes into the active render target
    private void DrawVolumes(List<FogVolume> volumes, ref RenderingData renderingData)
    {
        List<NativeLight> lights = SetupLights(ref renderingData);

        int perObjectLightCount = renderingData.lightData.maxPerObjectAdditionalLightsCount;

        for (int i = 0; i < volumes.Count; i++)
            volumes[i].DrawVolume(ref renderingData, commandBuffer, fogShader, lights, perObjectLightCount);
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
    #else
        var cameraColor = renderer.cameraColorTarget;
    #endif

        commandBuffer = CommandBufferPool.Get("Volumetric Fog Pass");

        DownsampleDepthBuffer();

        InitFogRenderTarget(renderingData.cameraData.camera);
        
        DrawVolumes(fogVolumes, ref renderingData);

        SetReprojectionBuffer(ref renderingData);

        BilateralBlur(descriptor.width, descriptor.height);
        BlendFog(cameraColor, ref renderingData);

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


    // Additively blend the fog volumes with the scene
    private void BlendFog(RenderTargetIdentifier target, ref RenderingData data)
    {
        commandBuffer.GetTemporaryRT(tempId, data.cameraData.cameraTargetDescriptor);
        commandBuffer.Blit(target, tempHandle);

        commandBuffer.SetGlobalTexture("_BlitSource", tempHandle);
        commandBuffer.SetGlobalTexture("_BlitAdd", volumeFogTexture);

        // Use blit add kernel to merge target color and the light buffer
        TargetBlit(commandBuffer, target, blitAdd, 0);
 
        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    // Basically a blit without the source texture
    private static void TargetBlit(CommandBuffer cmd, RenderTargetIdentifier destination, Material material, int pass)
    {
        cmd.SetRenderTarget(destination);
        cmd.DrawMesh(MeshUtility.FullscreenMesh, Matrix4x4.identity, material, 0, pass);
    }


    // Blurs the active resolution texture, and upscales and blits if needed
    private void BilateralBlur(int width, int height)
    {
        commandBuffer.SetKeyword(fullResKernel, Resolution == VolumetricResolution.Full);
        commandBuffer.SetKeyword(halfResKernel, Resolution == VolumetricResolution.Half);
        commandBuffer.SetKeyword(quarterResKernel, Resolution == VolumetricResolution.Quarter);

        // Blur quarter-res texture and upsample to full res
        if (Resolution == VolumetricResolution.Quarter)
        {
            BilateralBlur(quarterVolumeFogTexture, quarterDepthTarget, width / 4, height / 4); 
            Upsample(quarterVolumeFogTexture, quarterDepthTarget, volumeFogTexture);
            return;
        }
        
        // Blur half-res texture and upsample to full res
        if (Resolution == VolumetricResolution.Half)
        {
            BilateralBlur(halfVolumeFogTexture, halfDepthTarget, width / 2, height / 2);
            Upsample(halfVolumeFogTexture, halfDepthTarget, volumeFogTexture);
            return;
        }

        if (feature.enableReprojection && !feature.reprojectionBlur)
            return;

        // Blur full-scale texture 
        BilateralBlur(volumeFogTexture, null, width, height);
    }


    // Blurs source texture with provided depth texture- uses camera depth if null
    private void BilateralBlur(RenderTargetIdentifier source, RenderTargetIdentifier? depthBuffer, int sourceWidth, int sourceHeight)
    {
        commandBuffer.GetTemporaryRT(tempId, sourceWidth, sourceHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

        SetDepthTexture("_DepthTexture", depthBuffer);

        // Horizontal blur
        commandBuffer.SetGlobalTexture("_BlurSource", source);
        TargetBlit(commandBuffer, tempHandle, bilateralBlur, 0);

        // Vertical blur
        commandBuffer.SetGlobalTexture("_BlurSource", tempHandle);
        TargetBlit(commandBuffer, source, bilateralBlur, 1);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    // Downsamples depth texture to active resolution buffer
    private void DownsampleDepthBuffer()
    {
        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
        {
            SetDepthTexture("_DownsampleSource", null);
            TargetBlit(commandBuffer, halfDepthTarget, bilateralBlur, 2);
        }

        if (Resolution == VolumetricResolution.Quarter)
        {
            SetDepthTexture("_DownsampleSource", halfDepthTarget);
            TargetBlit(commandBuffer, quarterDepthTarget, bilateralBlur, 2);
        }
    }


    // Perform depth-aware upsampling to the destination
    private void Upsample(RenderTargetIdentifier sourceColor, RenderTargetIdentifier sourceDepth, RenderTargetIdentifier destination)
    {
        commandBuffer.SetGlobalTexture("_DownsampleColor", sourceColor);
        commandBuffer.SetGlobalTexture("_DownsampleDepth", sourceDepth);

        TargetBlit(commandBuffer, destination, bilateralBlur, 3);
    }


    // Use shader variants to either 
    // 1: Use the depth texture being assigned 
    // 2: Use the builtin _CameraDepthTexture property
    private void SetDepthTexture(string textureId, RenderTargetIdentifier? depth)
    {
        commandBuffer.SetKeyword(fullDepthSource, !depth.HasValue);

        if (depth.HasValue)
            commandBuffer.SetGlobalTexture(textureId, depth.Value);
    }


    // Set the current and previous matrices neccesary to generate motion vectors, since the unity builtins aren't reliable in edit mode
    private void SetReprojectionMatrices(Camera cam)
    {
        Matrix4x4 vpMatrix = cam.worldToCameraMatrix * cam.projectionMatrix;
        Matrix4x4 invVpMatrix = vpMatrix.inverse;

        commandBuffer.SetGlobalMatrix("_PrevView", prevVMatrix);
        commandBuffer.SetGlobalMatrix("_PrevViewProjection", prevVpMatrix);
        commandBuffer.SetGlobalMatrix("_PrevInvViewProjection", prevInvVpMatrix);

        commandBuffer.SetGlobalMatrix("_CameraView", cam.worldToCameraMatrix);
        commandBuffer.SetGlobalMatrix("_CameraViewProjection", vpMatrix);
        commandBuffer.SetGlobalMatrix("_InverseViewProjection", invVpMatrix);

        prevVMatrix = cam.worldToCameraMatrix;
        prevVpMatrix = vpMatrix;
        prevInvVpMatrix = invVpMatrix;
    }   


    // Set the volumetric fog render target
    // Clear the target if there is nothing to reproject
    // Otherwise, reproject the previous frame
    private void InitFogRenderTarget(Camera cam)
    {
        commandBuffer.SetKeyword(reprojectionKeyword, feature.enableReprojection);

        if (!feature.enableReprojection || reprojectionBuffer == null || !reprojectionBuffer.IsCreated())
        {
            if (Resolution == VolumetricResolution.Quarter)
                commandBuffer.SetRenderTarget(quarterVolumeFogTexture);
            else if (Resolution == VolumetricResolution.Half)
                commandBuffer.SetRenderTarget(halfVolumeFogTexture);
            else
                commandBuffer.SetRenderTarget(volumeFogTexture);

            commandBuffer.ClearRenderTarget(true, true, Color.black);
            return;
        }

        SetReprojectionMatrices(cam);

        temporalPass = (temporalPass + 1) % feature.temporalPassCount;
        commandBuffer.SetGlobalInt("_TemporalPassCount", feature.temporalPassCount);
        commandBuffer.SetGlobalInt("_TemporalPass", temporalPass);

        commandBuffer.SetGlobalTexture("_ReprojectSource", reprojectionBuffer);

        // TargetBlit will set the RenderTarget for us
        TargetBlit(commandBuffer, volumeFogTexture, reprojection, 0);
    }


    // Create the reprojection buffer from the current frame's texture to use next frame
    private void SetReprojectionBuffer(ref RenderingData data)
    {
        if (!feature.enableReprojection)
            return;

        RenderTextureDescriptor descriptor = data.cameraData.cameraTargetDescriptor;
        int width = descriptor.width;
        int height = descriptor.height;
        descriptor.colorFormat = RenderTextureFormat.ARGBHalf;

        if (reprojectionBuffer == null || !reprojectionBuffer.IsCreated() || reprojectionBuffer.width != width || reprojectionBuffer.height != height)
        {
            if (reprojectionBuffer != null && reprojectionBuffer.IsCreated())
                reprojectionBuffer.Release();

            reprojectionBuffer = new RenderTexture(descriptor);
            reprojectionBuffer.Create();
        }

        commandBuffer.CopyTexture(volumeFogTexture, 0, 0, reprojectionBuffer, 0, 0);
    }


    public void Dispose()
    {
        if (reprojectionBuffer != null && reprojectionBuffer.IsCreated())
            reprojectionBuffer.Release();
    }
}
