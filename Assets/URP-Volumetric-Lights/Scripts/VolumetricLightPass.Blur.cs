using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class VolumetricLightPass
{
    private static Material bilateralBlur;

    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");

    private RTHandle tempHandle;


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


    private void BilateralBlur(RTHandle source, RTHandle depthBuffer, int ratio = 1)
    {
        RenderTextureDescriptor desc = new(source.referenceSize.x, source.referenceSize.y, RenderTextureFormat.ARGBHalf, 0);

        RenderingUtils.ReAllocateIfNeeded(ref tempHandle, Vector2.one / ratio, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_Temp");
        
        LightPassBuffer.SetGlobalTexture("_DepthTexture", depthBuffer ?? depthAttachmentHandle);

        // Horizontal bilateral blur
        LightPassBuffer.SetGlobalTexture("_BlurSource", source);
        LightPassBuffer.Blit(null, tempHandle, bilateralBlur, 0); 

        // Vertical bilateral blur
        LightPassBuffer.SetGlobalTexture("_BlurSource", tempHandle);
        LightPassBuffer.Blit(null, source, bilateralBlur, 1);    
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



    private void DownsampleDepth(RTHandle source, RTHandle destination) 
    {
        LightPassBuffer.SetGlobalTexture("_DownsampleSource", source ?? depthAttachmentHandle);
        LightPassBuffer.Blit(null, destination, bilateralBlur, 2);
    }


    private void Upsample(RTHandle sourceColor, RTHandle sourceDepth, RTHandle destination)
    {
        LightPassBuffer.SetGlobalTexture("_FullResDepth", depthAttachmentHandle);
        LightPassBuffer.SetGlobalTexture("_DownsampleColor", sourceColor);
        LightPassBuffer.SetGlobalTexture("_DownsampleDepth", sourceDepth);

        LightPassBuffer.Blit(null, destination, bilateralBlur, 3);
    }
}
