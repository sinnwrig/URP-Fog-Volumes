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

    private static Material volumeLightMat;


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

        if (activeLights.Count > 0)
        {
            Vector3 apex = activeLights[0].transform.position;
            Vector3 axis = activeLights[0].transform.forward;

            float angle = (activeLights[0].coneAngle + 1) * 0.5f * Mathf.Deg2Rad;
            float dist = activeLights[0].coneLength;
            float radius = dist * Mathf.Tan(angle);
            float cosAngle = Mathf.Cos(angle);

            // update material
            commandBuffer.SetGlobalFloat("_PlaneDist", dist);
            commandBuffer.SetGlobalFloat("_BaseRadius", radius);        
            commandBuffer.SetGlobalFloat("_CosAngle", cosAngle);
            commandBuffer.SetGlobalVector("_ConeApex", apex);
            commandBuffer.SetGlobalVector("_ConeAxis", axis);

            commandBuffer.SetGlobalTexture("_SceneColor", source);
            commandBuffer.Blit(volumeLightTexture, source, volumeLightMat, 5);   
        }

        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }
}
