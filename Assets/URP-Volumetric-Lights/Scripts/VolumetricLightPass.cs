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

        VolumetricLightPass.volumetricLight = volumetricLight;

        ValidateResources();
    }   


    private void ValidateResources()
    {
        if (defaultLit == null)
            defaultLit = new Material(Shader.Find("Universal Render Pipeline/Lit"));

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

        DownsampleDepthBuffer();

        UpdateShaderParameters();

        for (int i = 0; i < activeLights.Count; i++)
        {
            activeLights[i].PreRenderEvent(this);
        }

        context.ExecuteCommandBuffer(LightPassBuffer);

        CommandBufferPool.Release(LightPassBuffer);
    }


    private void DownsampleDepthBuffer()
    {
        // Downsample depth buffer
        if (resolution == VolumetricResolution.Quarter)
        {
            // Downsample to half and then quarter
            DownsampleDepth(null, halfDepthBuffer);
            DownsampleDepth(halfDepthBuffer, quarterDepthBuffer);
        }
        else if (resolution == VolumetricResolution.Half)
        {
            // Downsample to half
            DownsampleDepth(null, halfDepthBuffer);
        }
    }


    public void DrawTestMesh(Mesh mesh, Matrix4x4 world)
    {
        LightPassBuffer.DrawMesh(mesh, world, defaultLit);
    }
}
