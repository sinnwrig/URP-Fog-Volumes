using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    // Global keywords

    public static readonly GlobalKeyword noiseKeyword = GlobalKeyword.Create("NOISE_ENABLED");
    public static readonly GlobalKeyword lightingKeyword = GlobalKeyword.Create("LIGHTING_ENABLED");
    public static readonly GlobalKeyword shadowsKeyword = GlobalKeyword.Create("SHADOWS_ENABLED");

    public static readonly GlobalKeyword reprojectionKeyword = GlobalKeyword.Create("TEMPORAL_REPROJECTION_ENABLED");


    // Blur keywords
    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");

    private static readonly GlobalKeyword fullDepthSource = GlobalKeyword.Create("SOURCE_FULL_DEPTH");
}
