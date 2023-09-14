using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;


public partial class VolumetricLightPass : ScriptableRenderPass
{
    public VolumetricResolution resolution;

    private static Material bilateralBlur;
    private static Material blitAdd;
    private static Shader volumetricLight;


    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;

    private CommandBuffer commandBuffer;


    private static readonly List<VolumetricLight> activeLights = new();


    public static void AddVolumetricLight(VolumetricLight light)
    {
        if (!activeLights.Contains(light))
        {
            activeLights.Add(light);
            light.Setup(volumetricLight);
        }
    }

    public static void RemoveVolumetricLight(VolumetricLight light)
    {
        activeLights.Remove(light);
    }
    


    public VolumetricLightPass(Shader blur, Shader add, Shader light)
    {
        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (blitAdd == null || blitAdd.shader != add)
            blitAdd = new Material(add);

        volumetricLight = light;

        ValidateResources();
    }   


    private void ValidateResources()
    {
        if (noiseTexture == null)
            noiseTexture = Resources.Load("Noise3DTexture") as Texture3D;

        if (ditherTexture == null)
            ditherTexture = Resources.Load("DitherTex") as Texture2D;
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

        commandBuffer = CommandBufferPool.Get("Volumetric Light Pass");

        DownsampleDepthBuffer();

        commandBuffer.Blit(source, VolumeLightBuffer);

        BilateralBlur(descriptor.width, descriptor.height);

        commandBuffer.Blit(volumeLightTexture, source);   

        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }
}
