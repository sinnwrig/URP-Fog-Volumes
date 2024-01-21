using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    private static Material bilateralBlur;
    private static Material fogMaterial;


    private VolumetricFogFeature feature;
    private CommandBuffer commandBuffer;
    


    public VolumetricFogPass(VolumetricFogFeature feature, Shader blur, Shader fog)
    {
        this.feature = feature;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (fogMaterial == null || fogMaterial.shader != fog)
            fogMaterial = new Material(fog);
    }   


    // Allocate temporary textures
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
    {
        InitializeRenderTargets(cmd, ref data);
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.postProcessEnabled)
            return;

        var fogVolumes = SetupVolumes(ref renderingData);
        if (fogVolumes.Count == 0)
            return;

        var renderer = renderingData.cameraData.renderer;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

    #if UNITY_2022_1_OR_NEWER
        var cameraColor = renderer.cameraColorTargetHandle;
        var cameraDepth = renderer.cameraDepthTargetHandle;
    #else
        var cameraColor = renderer.cameraColorTarget;
        var cameraDepth = renderer.cameraDepthTarget;
    #endif

        commandBuffer = CommandBufferPool.Get("Volumetric Fog Pass");

        DownsampleDepthBuffer();

        SetVolumeFogTarget(renderingData.cameraData.camera);
        DrawVolumes(fogVolumes, ref renderingData);
        SetupReprojectionTexture(ref renderingData);

        BilateralBlur(descriptor.width, descriptor.height);
        BlendFog(cameraColor);

        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    // Release temporary textures
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        ReleaseRenderTargets(cmd);
    }


    // Additive blend the fog volume with the scene
    private void BlendFog(RTHandle target)
    {
        commandBuffer.GetTemporaryRT(tempId, target.rt.descriptor);
        commandBuffer.Blit(target, tempHandle);

        commandBuffer.SetGlobalTexture("_BlitSource", tempHandle);
        commandBuffer.SetGlobalTexture("_BlitAdd", volumeFogTexture);

        // Use blit add kernel to merge target color and the light buffer
        TargetBlit(commandBuffer, target, fogMaterial, 0);
 
        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private static void TargetBlit(CommandBuffer cmd, RenderTargetIdentifier destination, Material material, int pass)
    {
        cmd.SetRenderTarget(destination);
        cmd.DrawMesh(MeshUtility.FullscreenMesh, Matrix4x4.identity, material, 0, pass);
    }
}
