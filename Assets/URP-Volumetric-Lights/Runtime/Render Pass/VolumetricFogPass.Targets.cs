using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    // Render Targets

    // Depth render targets
    private static readonly int halfDepthId = Shader.PropertyToID("_HalfDepthTarget");
    private static readonly RenderTargetIdentifier halfDepthTarget = new(halfDepthId);
    private static readonly int quarterDepthId = Shader.PropertyToID("_QuarterDepthTarget");
    private static readonly RenderTargetIdentifier quarterDepthTarget = new(quarterDepthId);

    // Light render targets
    private static readonly int volumeFogId = Shader.PropertyToID("_VolumeFogTexture");
    private static readonly RenderTargetIdentifier volumeFogTexture = new(volumeFogId);
    private static readonly int halfVolumeFogId = Shader.PropertyToID("_HalfVolumeFogTexture");
    private static readonly RenderTargetIdentifier halfVolumeFogTexture = new(halfVolumeFogId);
    private static readonly int quarterVolumeFogId = Shader.PropertyToID("_QuarterVolumeFogTexture");
    private static readonly RenderTargetIdentifier quarterVolumeFogTexture = new(quarterVolumeFogId);

    // Temp render target 
    private static readonly int tempId = Shader.PropertyToID("_Temp");
    private RenderTargetIdentifier tempHandle = new(tempId);


    private VolumetricResolution Resolution
    {
        get
        {
            // Temporal reprojection will force full-resolution
            if (feature.resolution != VolumetricResolution.Full && feature.enableReprojection)
                return VolumetricResolution.Full;
            
            return feature.resolution;
        }   
    }
}
