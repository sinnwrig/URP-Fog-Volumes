using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;


public partial class VolumetricLightPass : ScriptableRenderPass
{
    public VolumetricResolution resolution;

    private static Material blitAdd;
    private static Shader volumetricLight;

    
    private static Material defaultLit;

    public static Mesh spotLightMesh {get; private set; }
    public static Mesh pointLightMesh  {get; private set; }
    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;


    public CommandBuffer LightPassBuffer { get; private set; }


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
    


    public VolumetricLightPass(Shader bilateralBlur, Shader blitAdd, Shader volumetricLight)
    {
        if (VolumetricLightPass.bilateralBlur == null || VolumetricLightPass.bilateralBlur.shader != bilateralBlur)
            VolumetricLightPass.bilateralBlur = new Material(bilateralBlur);

        if (VolumetricLightPass.blitAdd == null || VolumetricLightPass.blitAdd.shader != blitAdd)
            VolumetricLightPass.blitAdd = new Material(blitAdd);

        defaultLit = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        VolumetricLightPass.volumetricLight = volumetricLight;

        if (spotLightMesh == null) 
            spotLightMesh = MeshUtility.CreateConeMesh(16);

        if (pointLightMesh == null)
            pointLightMesh = MeshUtility.CreateIcosphere(7);

        if (noiseTexture == null)
            noiseTexture = Resources.Load("Noise3DTexture") as Texture3D;

        if (ditherTexture == null)
            ditherTexture = Resources.Load("DitherTex") as Texture2D;
    }   


    private void UpdateShaderParameters()
    {
        LightPassBuffer.SetGlobalTexture("_DitherTexture", ditherTexture);
        LightPassBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

        LightPassBuffer = CommandBufferPool.Get("Volumetric Light Pass");

        if (resolution == VolumetricResolution.Quarter)
        {
            // Downsample to half and then quarter
            DownsampleDepth(default, halfDepthBuffer);
            DownsampleDepth(halfDepthBuffer, quarterDepthBuffer);
        }
        else if (resolution == VolumetricResolution.Half)
        {
            // Downsample to half
            DownsampleDepth(default, halfDepthBuffer);
        }


        UpdateShaderParameters();


        //for (int i = 0; i < activeLights.Count; i++)
        //{
        //    activeLights[i].PreRenderEvent(this);
        //}


        RenderTarget sourceClone = new("_SourceClone");
        sourceClone.GetTemporary(LightPassBuffer, renderingData.cameraData.cameraTargetDescriptor, FilterMode.Point);

        LightPassBuffer.Blit(source, sourceClone.identifier);
        
        BilateralBlur(sourceClone, default);

        LightPassBuffer.Blit(sourceClone.identifier, source);

        //LightPassBuffer.Blit(volumeLightTexture.identifier, source);

        //BilateralBlur(descriptor.width, descriptor.height);

        // add volume light buffer to rendered scene
        //LightPassBuffer.SetGlobalTexture("_Source", sourceClone.identifier);
        //LightPassBuffer.SetGlobalTexture("_SourceAdd", volumeLightTexture.identifier);

        //LightPassBuffer.Blit(sourceClone.identifier, source, blitAdd, 0);

        sourceClone.ReleaseTemporary(LightPassBuffer);

        context.ExecuteCommandBuffer(LightPassBuffer);

        CommandBufferPool.Release(LightPassBuffer);
    }
}
