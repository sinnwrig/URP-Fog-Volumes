using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using System;

public partial class VolumetricLightPass : ScriptableRenderPass
{
    public VolumetricResolution resolution;

    private static Material bilateralBlur;
    private static Material blitAdd;
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

        //commandBuffer.Blit(source, VolumeLightBuffer);

        //BilateralBlur(descriptor.width, descriptor.height);

        // Inverse object transform matrices.
        commandBuffer.SetGlobalMatrix("_Sphere", feature.sphereMatrix.Matrix.inverse);
        commandBuffer.SetGlobalMatrix("_Cylinder", feature.cylinderMatrix.Matrix.inverse);
        commandBuffer.SetGlobalMatrix("_Cone", feature.coneMatrix.Matrix.inverse);
        commandBuffer.SetGlobalMatrix("_Box", feature.boxMatrix.Matrix.inverse);

        commandBuffer.SetGlobalTexture("_SceneColor", source);
        commandBuffer.Blit(volumeLightTexture, source, volumeLightMat);   

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
