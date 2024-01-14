using UnityEngine;

public struct NativeLight
{
    public float range;
    public int shadowIndex;
    public Vector4 position;
    public Vector4 color;
    public Vector4 attenuation;
    public Vector4 spotDirection;
    public Rect screenRect;
}