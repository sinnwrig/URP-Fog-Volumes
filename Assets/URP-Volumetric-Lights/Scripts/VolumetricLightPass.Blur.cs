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
        if (resolution == VolumetricResolution.Quarter)
        {
            SetSingleKeyword(quarterResKernel); 
            
            BilateralBlur(quarterVolumeLightTexture, quarterDepthBuffer, width / 4, height / 4); 
            
            // Upscale to full res
            Upsample(quarterVolumeLightTexture, quarterDepthBuffer, volumeLightTexture);
        }
        else if (resolution == VolumetricResolution.Half)
        {
            SetSingleKeyword(halfResKernel);

            BilateralBlur(halfVolumeLightTexture, halfDepthBuffer, width / 2, height / 2);

            // Upscale to full res
            Upsample(halfVolumeLightTexture, halfDepthBuffer, volumeLightTexture);
        }
        else
        {
            SetSingleKeyword(fullResKernel);

            // Blur full-scale texture- use full-scale depth texture from shader
            BilateralBlur(volumeLightTexture, null, width, height);
        }
    }


    // Blurs source texture with provided depth texture to preserve edges- uses camera depth texture if none is provided
    private void BilateralBlur(RenderTargetIdentifier source, RenderTargetIdentifier? depthBuffer, int sourceWidth, int sourceHeight)
    {
        commandBuffer.GetTemporaryRT(tempId, sourceWidth, sourceHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

        SetDepthTexture("_DepthTexture", depthBuffer);

        // Horizontal bilateral blur
        commandBuffer.SetGlobalTexture("_BlurSource", source);
        commandBuffer.Blit(null, tempHandle, bilateralBlur, 0);

        // Vertical bilateral blur
        commandBuffer.SetGlobalTexture("_BlurSource", tempHandle);
        commandBuffer.Blit(null, source, bilateralBlur, 1);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private void SetSingleKeyword(GlobalKeyword keyword)
    {
        commandBuffer.EnableKeyword(keyword);

        if (keyword.name != fullResKernel.name)
            commandBuffer.DisableKeyword(fullResKernel);

        if (keyword.name != halfResKernel.name)
            commandBuffer.DisableKeyword(halfResKernel);

        if (keyword.name != quarterResKernel.name)
            commandBuffer.DisableKeyword(quarterResKernel);
    }


    // Downsamples depth texture to active resolution buffer
    private void DownsampleDepthBuffer()
    {
        // Downsample depth buffer
        if (resolution == VolumetricResolution.Half || resolution == VolumetricResolution.Quarter)
        {
            // Downsample full depth to half
            DownsampleDepth(null, halfDepthBuffer);
        }

        if (resolution == VolumetricResolution.Quarter)
        {
            // Downsample half depth to quarter
            DownsampleDepth(halfDepthBuffer, quarterDepthBuffer);
        }
    }


    private void DownsampleDepth(RenderTargetIdentifier? source, RenderTargetIdentifier destination)
    {
        SetDepthTexture("_DownsampleSource", source);
        commandBuffer.Blit(null, destination, bilateralBlur, 2);
    }


    private void Upsample(RenderTargetIdentifier sourceColor, RenderTargetIdentifier sourceDepth, RenderTargetIdentifier destination)
    {
        commandBuffer.SetGlobalTexture("_DownsampleColor", sourceColor);
        commandBuffer.SetGlobalTexture("_DownsampleDepth", sourceDepth);

        commandBuffer.Blit(null, destination, bilateralBlur, 3);
    }


    // Use shader variants to either 
    // 1: Use the depth texture being assigned 
    // 2: Use the _CameraDepthTexture property if the texture is null
    private void SetDepthTexture(string textureId, RenderTargetIdentifier? depth)
    {
        commandBuffer.SetKeyword(fullDepthSource, !depth.HasValue);

        if (depth.HasValue)
        {
            commandBuffer.SetGlobalTexture(textureId, depth.Value);
        }
    }
}
