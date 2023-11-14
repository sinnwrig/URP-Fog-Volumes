using UnityEngine;
using UnityEngine.Rendering;


public struct SortedLight
{
    public VisibleLight visibleLight;
    public VolumetricLight volumeLight;
    public int index;
    public Vector4 position;
    public Vector4 color;
    public Vector4 attenuation;
    public Vector4 spotDirection;
}