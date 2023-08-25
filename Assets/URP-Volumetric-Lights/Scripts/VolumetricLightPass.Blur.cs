using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Rendering;

public partial class VolumetricLightPass
{
    private static Material bilateralBlur;

    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");



    private void BilateralBlur()
    {
        if (resolution == VolumetricResolution.Quarter)
        {
            SetSingleKeyword(quarterResKernel);

            BilateralBlur(quarterVolumeLightTexture, quarterDepthBuffer, 4);

            // Upscale to full res
            Upsample(quarterVolumeLightTexture, quarterDepthBuffer, volumeLightTexture);
        }
        else if (resolution == VolumetricResolution.Half)
        {
            SetSingleKeyword(halfResKernel);

            BilateralBlur(halfVolumeLightTexture, halfDepthBuffer, 2);
            
            // Upscale to full res
            Upsample(halfVolumeLightTexture, halfDepthBuffer, volumeLightTexture);
        }
        else
        {
            SetSingleKeyword(fullResKernel);

            // Blur full-scale texture
            BilateralBlur(volumeLightTexture, default);
        }
    }


    private void BilateralBlur(RenderTarget source, RenderTarget depthBuffer, int ratio = 1)
    {
        RenderTarget temp = new("_Temp");
        temp.GetTemporary(LightPassBuffer, source.descriptor.width / ratio, source.descriptor.height / ratio, 0, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear);

        LightPassBuffer.SetGlobalTexture("_DepthTexture", depthBuffer.isAssigned ? depthBuffer.identifier : depthAttachmentHandle);

        // Horizontal bilateral blur
        LightPassBuffer.SetGlobalTexture("_BlurSource", source.identifier);
        LightPassBuffer.Blit(null, temp.identifier, bilateralBlur, 0); 

        // Vertical bilateral blur
        LightPassBuffer.SetGlobalTexture("_BlurSource", temp.identifier);
        LightPassBuffer.Blit(null, source.identifier, bilateralBlur, 1); 

        temp.ReleaseTemporary(LightPassBuffer);    
    }

    
    private void SetSingleKeyword(GlobalKeyword keyword)
    {
        LightPassBuffer.EnableKeyword(keyword);

        if (keyword.name != fullResKernel.name) 
            LightPassBuffer.DisableKeyword(fullResKernel);

        if (keyword.name != halfResKernel.name) 
            LightPassBuffer.DisableKeyword(halfResKernel);

        if (keyword.name != quarterResKernel.name) 
            LightPassBuffer.DisableKeyword(quarterResKernel);
    }



    private void DownsampleDepth(RenderTarget source, RenderTarget destination) 
    {
        LightPassBuffer.SetGlobalTexture("_DownsampleSource", source.isAssigned ? source.identifier : depthAttachmentHandle);
        LightPassBuffer.Blit(null, destination.identifier, bilateralBlur, 2);
    }


    private void Upsample(RenderTarget sourceColor, RenderTarget sourceDepth, RenderTarget destination)
    {
        LightPassBuffer.SetGlobalTexture("_FullResDepth", depthAttachmentHandle);
        LightPassBuffer.SetGlobalTexture("_DownsampleColor", sourceColor.identifier);
        LightPassBuffer.SetGlobalTexture("_DownsampleDepth", sourceDepth.identifier);

        LightPassBuffer.Blit(null, destination.identifier, bilateralBlur, 3);
    }
}
