using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricLightPass
{
    private RTHandle halfDepthBuffer;
    private RTHandle quarterDepthBuffer;

    private RTHandle volumeLightTexture;
    private RTHandle halfVolumeLightTexture;
    private RTHandle quarterVolumeLightTexture;


    public RTHandle VolumeLightBuffer 
    {
        get 
        {
            return resolution switch
            {
                VolumetricResolution.Quarter => quarterVolumeLightTexture,
                VolumetricResolution.Half => halfVolumeLightTexture,
                VolumetricResolution.Full => volumeLightTexture,
                _ => volumeLightTexture,
            };
        }
    }

    public RTHandle VolumeLightDepthBuffer
    {
        get 
        {
            return resolution switch
            {
                VolumetricResolution.Quarter => quarterDepthBuffer,
                VolumetricResolution.Half => halfDepthBuffer,
                VolumetricResolution.Full => depthAttachmentHandle,
                _ => depthAttachmentHandle
            };
        }
    }


    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
    
        RenderingUtils.ReAllocateIfNeeded(ref volumeLightTexture, Vector2.one, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_FullResColor");

        if (resolution == VolumetricResolution.Half || resolution == VolumetricResolution.Quarter)
        {
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            RenderingUtils.ReAllocateIfNeeded(ref halfVolumeLightTexture, Vector2.one / 2, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_HalfResColor");
            
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateIfNeeded(ref halfDepthBuffer, Vector2.one / 2, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_HalfResDepthBuffer");
        }

        if (resolution == VolumetricResolution.Quarter)
        {
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            RenderingUtils.ReAllocateIfNeeded(ref quarterVolumeLightTexture, Vector2.one / 4, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_QuarterResColor");

            descriptor.colorFormat = RenderTextureFormat.RFloat;
            RenderingUtils.ReAllocateIfNeeded(ref quarterDepthBuffer, Vector2.one / 4, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_QuarterResDepthBuffer");
        }
    }


    public void Dispose()
    {
        halfDepthBuffer?.Release();
        quarterDepthBuffer?.Release();

        volumeLightTexture?.Release();
        halfVolumeLightTexture?.Release();
        quarterVolumeLightTexture?.Release();

        // Defined in VolumetricLightPass.Blur.cs
        tempHandle?.Release();
    }
}
