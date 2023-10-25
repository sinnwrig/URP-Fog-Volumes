using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System;

public partial class VolumetricLightPass : ScriptableRenderPass
{
    public VolumetricResolution resolution;

    private static Material bilateralBlur;
    private static Shader volumetricLight;

    private static Material volumeLightMat;


    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;

    private CommandBuffer commandBuffer;

    public VolumetricLightFeature feature;


    private static readonly List<VolumetricLight> activeLights = new();


    public static void AddVolumetricLight(VolumetricLight light)
    {
        if (!activeLights.Contains(light))
        {
            activeLights.Add(light);

            if (volumetricLight != null) 
            {
                light.Setup(volumetricLight);
            }
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

        volumetricLight = light;

        foreach (VolumetricLight activeLight in activeLights)
        {
            activeLight.Setup(volumetricLight);
        }

        if (volumeLightMat == null || volumeLightMat.shader != light)
            volumeLightMat = new Material(light);

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

        // Blit for every active volumetric light
        //commandBuffer.Blit(source, volumeLightTexture, volumeLightMat);  


        // Blur and upsample volumetric light texture
        //BilateralBlur(descriptor.width, descriptor.height);

        //commandBuffer.SetGlobalTexture("_SourceTexture", sourceCopy);
        //commandBuffer.SetGlobalTexture("_SourceAdd", volumeLightTexture);
        // Use blit add kernel to merge source color and the blurred light texture
        //commandBuffer.Blit(null, source, 3);

        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private static Vector3 ClampMin(Vector3 vector)
    {
        vector.x = vector.x == 0.0 ? 1e-20f : vector.x;
        vector.y = vector.y == 0.0 ? 1e-20f : vector.y;
        vector.z = vector.z == 0.0 ? 1e-20f : vector.z;
        return vector;
    }
}
