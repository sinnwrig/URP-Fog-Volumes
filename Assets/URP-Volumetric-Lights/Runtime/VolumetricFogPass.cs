using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    // If full res:
    // 3 full-res color copies (blit to final texture, bilateral blur, fog fullres texture)

    // If half res:
    // 2 full-res color copies (blit to final texture, fog fullres texture)
    // 2 half-res color copies (bilateral blur, fog lowres texture)
    // 1 half-res depth copy

    // If quarter res:
    // 2 full-res color copies (blit to final texture, fog fullres texture)
    // 2 quarter-res color copies (bilateral blur, fog lowres texture)
    // 1 half-res depth copy
    // 1 quarter-res depth copy

    public static readonly GlobalKeyword noiseKeyword = GlobalKeyword.Create("NOISE_ENABLED");
    public static readonly GlobalKeyword lightingKeyword = GlobalKeyword.Create("LIGHTING_ENABLED");
    public static readonly GlobalKeyword shadowsKeyword = GlobalKeyword.Create("SHADOWS_ENABLED");


    // Blur keywords
    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");

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


    private static Material bilateralBlur;
    private static Material fogMaterial;


    public VolumetricFogFeature feature;
    public VolumetricResolution Resolution => feature.resolution;

    public CommandBuffer commandBuffer;


    // Active resolution target
    public RenderTargetIdentifier VolumeFogBuffer 
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
