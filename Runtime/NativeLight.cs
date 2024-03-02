using UnityEngine;

public struct NativeLight
{
    public bool isDirectional;
    public float range;
    public int shadowIndex;
    public Vector4 position;
    public Vector4 color;
    public Vector4 attenuation;
    public Vector4 spotDirection;
    public int layer;
    public Light light;
}