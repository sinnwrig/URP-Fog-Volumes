using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricLightPass
{
    // Depth buffers
    private static readonly int halfDepthId = Shader.PropertyToID("_HalfDepthBuffer");
    private static readonly RenderTargetIdentifier halfDepthBuffer = new(halfDepthId);
    private static readonly int quarterDepthId = Shader.PropertyToID("_QuarterDepthBuffer");
    private static readonly RenderTargetIdentifier quarterDepthBuffer = new(quarterDepthId);


    // Light color buffers
    private static readonly int volumeLightId = Shader.PropertyToID("_VolumeLightTexture");
    private static readonly RenderTargetIdentifier volumeLightTexture = new(volumeLightId);
    private static readonly int halfVolumeLightId = Shader.PropertyToID("_HalfVolumeLightTexture");
    private static readonly RenderTargetIdentifier halfVolumeLightTexture = new(halfVolumeLightId);
    private static readonly int quarterVolumeLightId = Shader.PropertyToID("_QuarterVolumeLightTexture");
    private static readonly RenderTargetIdentifier quarterVolumeLightTexture = new(quarterVolumeLightId);


    // Temp buffer
    private static readonly int tempId = Shader.PropertyToID("_Temp");
    private RenderTargetIdentifier tempHandle = new(tempId);


    // Active resolution light buffer
    public RenderTargetIdentifier VolumeLightBuffer 
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


    // Get required temporary textures
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
    {
        var descriptor = data.cameraData.cameraTargetDescriptor;

        cmd.GetTemporaryRT(volumeLightId, descriptor.width, descriptor.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

        if (resolution == VolumetricResolution.Half)
            cmd.GetTemporaryRT(halfVolumeLightId, descriptor.width / 2, descriptor.height / 2, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

        // Half/Quarter res both need half-res depth buffer for downsampling
        if (resolution == VolumetricResolution.Half || resolution == VolumetricResolution.Quarter) 
            cmd.GetTemporaryRT(halfDepthId, descriptor.width / 2, descriptor.height / 2, 0, FilterMode.Point, RenderTextureFormat.RFloat);

        if (resolution == VolumetricResolution.Quarter)
        {
            cmd.GetTemporaryRT(quarterVolumeLightId, descriptor.width / 4, descriptor.height / 4, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            cmd.GetTemporaryRT(quarterDepthId, descriptor.width / 4, descriptor.height / 4, 0, FilterMode.Point, RenderTextureFormat.RFloat);
        }
    }


    // Release temporary textures
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(volumeLightId);

        if (resolution == VolumetricResolution.Half)
            cmd.ReleaseTemporaryRT(halfVolumeLightId);

        if (resolution == VolumetricResolution.Half || resolution == VolumetricResolution.Quarter)
            cmd.ReleaseTemporaryRT(halfDepthId);

        if (resolution == VolumetricResolution.Quarter)
        {
            cmd.ReleaseTemporaryRT(quarterVolumeLightId);
            cmd.ReleaseTemporaryRT(quarterDepthId);
        }
    }
}
