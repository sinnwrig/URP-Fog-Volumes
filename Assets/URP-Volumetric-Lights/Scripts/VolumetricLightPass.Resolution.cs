using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricLightPass
{
    private static readonly RenderTarget halfDepthBuffer = new("_HalfResDepthBuffer");
    private static readonly RenderTarget quarterDepthBuffer = new("_QuarterResDepthBuffer");

    private static readonly RenderTarget volumeLightTexture = new("_FullResColor");
    private static readonly RenderTarget halfVolumeLightTexture = new("_HalfResColor");
    private static readonly RenderTarget quarterVolumeLightTexture = new("_QuarterResColor");


    public RenderTarget VolumeLightBuffer 
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

    public RenderTarget VolumeLightDepthBuffer
    {
        get 
        {
            return resolution switch
            {
                VolumetricResolution.Quarter => quarterDepthBuffer,
                VolumetricResolution.Half => halfDepthBuffer,
                VolumetricResolution.Full => new("_CameraDepthTexture"),
                _ => new("_CameraDepthTexture")
            };
        }
    }



    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;
        int width = descriptor.width;
        int height = descriptor.height;

        volumeLightTexture.GetTemporary(cmd, width, height, 0, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear);

        if (resolution == VolumetricResolution.Half || resolution == VolumetricResolution.Quarter)
        {
            halfVolumeLightTexture.GetTemporary(cmd, width / 2, height / 2, 0, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear);
            halfDepthBuffer.GetTemporary(cmd, width / 2, height / 2, 0, RenderTextureFormat.RFloat, FilterMode.Point);
        }

        if (resolution == VolumetricResolution.Quarter)
        {
            quarterVolumeLightTexture.GetTemporary(cmd, width / 4, height / 4, 0, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear);
            quarterDepthBuffer.GetTemporary(cmd, width / 4, height / 4, 0, RenderTextureFormat.RFloat, FilterMode.Point);
        }
    }


    public override void OnCameraCleanup(CommandBuffer cmd)
    {   
        volumeLightTexture.ReleaseTemporary(cmd);

        if (resolution == VolumetricResolution.Half || resolution == VolumetricResolution.Quarter)
        {
            halfVolumeLightTexture.ReleaseTemporary(cmd);
            halfDepthBuffer.ReleaseTemporary(cmd);
        }

        if (resolution == VolumetricResolution.Quarter)
        {
            quarterVolumeLightTexture.ReleaseTemporary(cmd);
            quarterDepthBuffer.ReleaseTemporary(cmd);
        }
    }
}
