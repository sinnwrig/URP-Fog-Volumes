using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class VolumetricLightPass
{
    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");

    private static readonly GlobalKeyword fullDepthSource = GlobalKeyword.Create("SOURCE_FULL_DEPTH");



    // Blurs the active resolution texture, then upscales and blits (if neccesary) to full resolution texture
    private void BilateralBlur(int width, int height)
    {
        if (Resolution == VolumetricResolution.Quarter)
        {
            commandBuffer.EnableKeyword(quarterResKernel);
            commandBuffer.DisableKeyword(fullResKernel);
            commandBuffer.DisableKeyword(halfResKernel);
            
            BilateralBlur(quarterVolumeLightTexture, quarterDepthTarget, width / 4, height / 4); 
            
            // Upsample to full res
            Upsample(quarterVolumeLightTexture, quarterDepthTarget, volumeLightTexture);
        }
        else if (Resolution == VolumetricResolution.Half)
        {
            commandBuffer.EnableKeyword(halfResKernel);
            commandBuffer.DisableKeyword(fullResKernel);
            commandBuffer.DisableKeyword(quarterResKernel);

            BilateralBlur(halfVolumeLightTexture, halfDepthTarget, width / 2, height / 2);

            // Upsample to full res
            Upsample(halfVolumeLightTexture, halfDepthTarget, volumeLightTexture);
        }
        else
        {
            commandBuffer.EnableKeyword(fullResKernel);
            commandBuffer.DisableKeyword(halfResKernel);
            commandBuffer.DisableKeyword(quarterResKernel);

            // Blur full-scale texture- use full-scale depth texture from shader
            BilateralBlur(volumeLightTexture, null, width, height);
        }
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
