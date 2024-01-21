using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    public enum VolumetricResolution
    {
        Full,
        Half,
        Quarter
    }


    // Allocate temporary textures
    private void InitializeRenderTargets(CommandBuffer cmd, ref RenderingData data)
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


    // Release temporary textures
    private void ReleaseRenderTargets(CommandBuffer cmd)
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

        if (feature.enableReprojection && !feature.reprojectionBlur)
            return;

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
    // 2: Use the _CameraDepthTexture property
    private void SetDepthTexture(string textureId, RenderTargetIdentifier? depth)
    {
        commandBuffer.SetKeyword(fullDepthSource, !depth.HasValue);

        if (depth.HasValue)
            commandBuffer.SetGlobalTexture(textureId, depth.Value);
    }
}
