using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricLightPass : ScriptableRenderPass
{
    public VolumetricLightFeature feature;
    public VolumetricResolution Resolution => feature.resolution;


    private static Material bilateralBlur;
    private static Material lightMaterial;


    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;

    public CommandBuffer commandBuffer;
    


    public VolumetricLightPass(VolumetricLightFeature feature, Shader blur, Shader light)
    {
        this.feature = feature;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (lightMaterial == null || lightMaterial.shader != light)
            lightMaterial = new Material(light);

        if (noiseTexture == null)
            noiseTexture = Resources.Load("Noise3DTexture") as Texture3D;

        if (ditherTexture == null)
            ditherTexture = Resources.Load("DitherTex") as Texture2D;
    }   


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var lights = GetSortedLights(ref renderingData);
        if (lights.Count == 0)
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

        commandBuffer = CommandBufferPool.Get("Volumetric Light Pass");
        commandBuffer.Clear();

        DownsampleDepthBuffer();
        DrawLights(lights);
        BilateralBlur(descriptor.width, descriptor.height);
        BlendLights(cameraColor);

        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    private void SetKeyword(GlobalKeyword keyword, params GlobalKeyword[] other)
    {
        commandBuffer.EnableKeyword(keyword);

        for (int i = 0; i < other.Length; i++)
            commandBuffer.DisableKeyword(other[i]);
    }


    private void ClearColor(CommandBuffer cmd, RenderTargetIdentifier rt, Color color)
    {
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(RTClearFlags.Color, color, 1, 0);
    }
}
