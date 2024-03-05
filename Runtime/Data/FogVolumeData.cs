using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

using System;

namespace Sinnwrig.FogVolumes
{
    [Serializable, ReloadGroup, CreateAssetMenu(menuName = "Fog Volume Data")]
    public sealed class FogVolumeData : ScriptableObject
    {
        /// <summary>
        /// The Bilateral Blur shader.
        /// </summary>
        [Reload("Shaders/BilateralBlur.shader")]
        public Shader bilateralBlur;

        /// <summary>
        /// The Volumetric Fog Post Processing shader.
        /// </summary>
        [Reload("Shaders/VolumetricFog.shader")]
        public Shader volumetricFog;

        /// <summary>
        /// The Reprojection Post Processing shader.
        /// </summary>
        [Reload("Shaders/Reprojection.shader")]
        public Shader reprojection;

        /// <summary>
        /// The Blit Add Post Processing shader.
        /// </summary>
        [Reload("Shaders/BlitAdd.shader")]
        public Shader blitAdd;
    }
}
