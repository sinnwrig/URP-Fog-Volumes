using UnityEngine;

namespace Sinnwrig.FogVolumes
{
    // Made internal since it's basically VisibleLight with extra useless values.
    internal struct NativeLight
    {
        internal bool isDirectional;
        internal float range;
        internal int shadowIndex;
        internal Vector4 position;
        internal Vector4 color;
        internal Vector4 attenuation;
        internal Vector4 spotDirection;
        internal int layer;
        internal float cameraDistance;
    }
}