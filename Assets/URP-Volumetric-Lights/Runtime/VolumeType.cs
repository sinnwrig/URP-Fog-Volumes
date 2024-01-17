using UnityEngine.Rendering;

/// <summary>
/// The primitive shape type to use when rendering a volume
/// </summary>
public enum VolumeType
{
    Sphere = 0, 
    Cube = 1,
    Capsule = 2, 
    Cylinder = 3, 
}


public static class VolumeTypeExtensions
{
    static readonly GlobalKeyword[] shapeKeywords = new GlobalKeyword[]
    {
        GlobalKeyword.Create("SPHERE_VOLUME"),
        GlobalKeyword.Create("CUBE_VOLUME"),
        GlobalKeyword.Create("CAPSULE_VOLUME"),
        GlobalKeyword.Create("CYLINDER_VOLUME")
    };


    /// <summary>
    /// Sets the approriate global shader keyword for this volume type
    /// </summary>
    /// <param name="cmd">The Command Buffer to use</param>
    public static void SetVolumeKeyword(this VolumeType type, CommandBuffer cmd)
    {
        for (int i = 0; i < 4; i++)
            cmd.SetKeyword(shapeKeywords[i], i == (int)type);
    }
}