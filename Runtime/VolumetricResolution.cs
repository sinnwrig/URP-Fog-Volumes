using UnityEngine.Rendering;

namespace Sinnwrig.FogVolumes
{ 
    /// <summary>
    /// The resolution to render volumetric fog at
    /// </summary>
    [System.Serializable]
    public enum VolumetricResolution
    {
        Full = 0,
        Half = 1,
        Quarter = 2
    }


    internal static class VolumetricResolutionExtensions
    {
        static readonly GlobalKeyword[] resolutionKeywords = new GlobalKeyword[]
        {
            GlobalKeyword.Create("FULL_RES_BLUR"),
            GlobalKeyword.Create("HALF_RES_BLUR"),
            GlobalKeyword.Create("QUARTER_RES_BLUR"),
        };


        internal static void SetResolutionKeyword(this VolumetricResolution type, CommandBuffer cmd)
        {
            for (int i = 0; i < resolutionKeywords.Length; i++)
                cmd.SetKeyword(resolutionKeywords[i], i == (int)type);
        }
    }
}