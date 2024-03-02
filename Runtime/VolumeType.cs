using UnityEngine.Rendering;

namespace Sinnwrig.FogVolumes
{
    /// <summary>
    /// The primitive shape type to use when rendering a volume
    /// </summary>
    [System.Serializable]
    public enum VolumeType
    {
        Sphere = 0, 
        Cube = 1,
        Capsule = 2, 
        Cylinder = 3, 
    }


    internal static class VolumeTypeExtensions
    {
        static readonly GlobalKeyword[] shapeKeywords = new GlobalKeyword[]
        {
            GlobalKeyword.Create("SPHERE_VOLUME"),
            GlobalKeyword.Create("CUBE_VOLUME"),
            GlobalKeyword.Create("CAPSULE_VOLUME"),
            GlobalKeyword.Create("CYLINDER_VOLUME")
        };


        public static void SetVolumeKeyword(this VolumeType type, CommandBuffer cmd)
        {
            for (int i = 0; i < shapeKeywords.Length; i++)
                cmd.SetKeyword(shapeKeywords[i], i == (int)type);
        }
    }
}