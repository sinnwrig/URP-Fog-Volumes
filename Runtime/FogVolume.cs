using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace Sinnwrig.FogVolumes
{
    [ExecuteAlways]
    public class FogVolume : MonoBehaviour 
    {
        /// <summary>
        /// The rendered shape of the volume.
        /// </summary>
        public VolumeType volumeType;

        /// <summary>
        /// How intensely the volume should fade from its edges determined by a nonlinear, normalized range.
        /// <para>Cube volumes use the [x, y, z] components for each local axis.</para> 
        /// <para>Cylinder volumes use the [x, y] components for radius and height.</para>
        /// <para>Capsule and Sphere volumes use the [x] components for radius.</para>
        /// </summary>
        public Vector3 edgeFade = Vector3.zero;

        /// <summary>
        /// The offset applied to the volume to determine where fading begins/ends.
        /// <para>Cube volumes use the [x, y, z] components in the range [-0.5, 0.5] to offset each local axis.</para> 
        /// <para>Cylinder volumes use the [y] component in the range [-1, 1] for height offset.</para>
        /// <para>Capsule and Sphere volumes ignore this setting.</para>
        /// </summary>
        public Vector3 fadeOffset = Vector3.zero;

        /// <summary>
        /// Determines whether or not fog lighting will be affected by fading along with base fog albedo.
        /// </summary>
        public bool lightsFade = false;

        /// <summary>
        /// The maximum render distance of the fog.
        /// </summary>
        [Min(0)] 
        public float maxDistance = 100.0f;

        /// <summary>
        /// The percentage from the maximum distance at which to begin fading fog.
        /// </summary>
        [Range(0, 1f)] 
        public float distanceFade = 1f;

        /// <summary>
        /// The mask used to determine what lights can affect this volume.
        /// </summary>
        public LayerMask lightLayerMask = ~0;

        /// <summary>
        /// Determines whether or not this volume should ignore the maximum per-object light limit set in the renderer. Absolute light limit is 32.
        /// </summary>
        public bool disableLightLimit;


        private FogVolumeProfile _internalProfile;

        /// <summary>
        /// The shared profile used by this volume.
        /// </summary>
        public FogVolumeProfile sharedProfile = null;

        /// <summary>
        /// The instantiated profile used by this volume.
        /// </summary>
        public FogVolumeProfile profile
        {
            get
            {
                if (_internalProfile == null)
                {
                    if (sharedProfile != null)
                        _internalProfile = Instantiate(sharedProfile);
                }

                return _internalProfile;
            }

            set => _internalProfile = value;
        }


        /// <summary>
        /// Is this volume using a shared profile or an instantiated one?
        /// </summary>
        public bool HasInstantiatedProfile() => _internalProfile != null;


        /// <summary>
        /// (Read-Only) The profile currently being used by this volume.
        /// </summary>
        public FogVolumeProfile ProfileReference => _internalProfile == null ? sharedProfile : _internalProfile;


        private MaterialPropertyBlock _propertyBlock;

        private MaterialPropertyBlock PropertyBlock
        {
            get
            {
                _propertyBlock ??= new MaterialPropertyBlock();

                return _propertyBlock;
            }
        }


        private const int maxLightCount = 32;
        private static readonly float[] lightsToShadow = new float[maxLightCount];
        private static readonly Vector4[] lightPositions  = new Vector4[maxLightCount];
        private static readonly Vector4[] lightColors = new Vector4[maxLightCount];
        private static readonly Vector4[] lightAttenuations = new Vector4[maxLightCount];
        private static readonly Vector4[] spotDirections = new Vector4[maxLightCount];


        private void OnEnable() => VolumetricFogPass.AddVolume(this);
        private void OnDisable() => VolumetricFogPass.RemoveVolume(this);
        private void OnDestroy() => OnDisable();


        private void SetupViewport(ref RenderingData renderingData)
        {
            Vector3[] boundsPoints = volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder ? BoundsUtility.capsuleCorners : BoundsUtility.cubeCorners;

            Vector4 viewport = BoundsUtility.GetViewportRect(transform.localToWorldMatrix, renderingData.cameraData.camera, boundsPoints);

            if (BoundsUtility.InsideBounds(transform.worldToLocalMatrix, renderingData.cameraData.camera.transform.position, GetBounds()))
                viewport = new Vector4(0, 0, 1, 1);

            PropertyBlock.SetVector("_ViewportRect", viewport);
        }


        private Vector3 EdgeFade(out Vector3 offset)
        {
            offset = Vector3.zero;

            switch (volumeType)
            {
                case VolumeType.Cube:
                    float edgeX = edgeFade.x * 2 - 1.5f;
                    float edgeY = edgeFade.y * 2 - 1.5f;
                    float edgeZ = edgeFade.z * 2 - 1.5f;

                    offset = fadeOffset;

                    return new Vector3(edgeX, edgeY, edgeZ);

                case VolumeType.Cylinder:
                    float cylX = edgeFade.x * 2 - 1.5f;
                    float cylY = edgeFade.y * 4 - 3;

                    offset.y = fadeOffset.y * 2;

                    return new Vector3(cylX, cylY, 0);

                default:
                    float sphereX = edgeFade.x - 0.5f;
                    return new Vector3(sphereX, 0, 0);
            }
        }


        // Upload the list of affecting lights to the shader
        private void SetupLighting(List<NativeLight> lights, int maxLights)
        {
            maxLights = disableLightLimit ? maxLightCount : Math.Min(maxLightCount, maxLights);

            PropertyBlock.SetVector("_EdgeFade", EdgeFade(out Vector3 offset));

            PropertyBlock.SetVector("_FadeOffset", offset);
            
            PropertyBlock.SetFloat("_LightsFade", lightsFade ? 1 : 0);

            if (ProfileReference.lightingMode != LightingMode.Unlit)
            {
                Bounds bounds = GetBounds();
                Matrix4x4 trs = transform.localToWorldMatrix;
                Matrix4x4 invTrs = transform.worldToLocalMatrix;

                int lightCount = 0;
                for (int i = 0; i < Math.Min(lights.Count, maxLights); i++)
                {   
                    NativeLight light = lights[i];

                    if (lightLayerMask != (lightLayerMask | (1 << light.layer)))
                        continue;

                    if (light.isDirectional || BoundsUtility.WithinDistance(trs, invTrs, light.position, light.range, bounds))
                    {
                        lightsToShadow[lightCount] = light.shadowIndex;
                        lightPositions[lightCount] = light.position;
                        lightColors[lightCount] = light.color;
                        lightAttenuations[lightCount] = light.attenuation;
                        spotDirections[lightCount] = light.spotDirection;

                        lightCount++;
                    }
                }

                PropertyBlock.SetInteger("_LightCount", lightCount);  

                // Initialize the shader light arrays
                PropertyBlock.SetFloatArray("_LightToShadowIndices", lightsToShadow);
                PropertyBlock.SetVectorArray("_LightPositions", lightPositions);   
                PropertyBlock.SetVectorArray("_LightColors", lightColors);        
                PropertyBlock.SetVectorArray("_LightAttenuations", lightAttenuations);  
                PropertyBlock.SetVectorArray("_SpotDirections", spotDirections); 
            }
        }


        // Not public since some global properties must be set for the shader to render correctly. 
        internal void DrawVolume(ref RenderingData renderingData, CommandBuffer cmd, Shader shader, List<NativeLight> lights, int maxLights)
        {
            PropertyBlock.SetVector("_FogRange", new Vector2(maxDistance, Mathf.Lerp(0, maxDistance, distanceFade - 0.001f)));

            Material material = ProfileReference.GetMaterial(shader, cmd);

            volumeType.SetVolumeKeyword(cmd);

            SetupViewport(ref renderingData);
            SetupLighting(lights, maxLights);

            PropertyBlock.SetMatrix("_InverseVolumeMatrix", transform.worldToLocalMatrix);

            cmd.DrawMesh(MeshUtility.FullscreenQuad, Matrix4x4.identity, material, 0, 0, PropertyBlock);
        }


        /// <summary>
        /// Determines if the volume should be culled given a camera position and the camera culling planes
        /// </summary>
        /// <param name="cameraPosition">The position of the camera to use in determining culling state.</param>
        /// <param name="cameraPlanes">The camera planes to use in determining culling state.</param>
        /// <returns>True if the volume shoud be culled, false otherwise.</returns>
        public bool CullVolume(Vector3 cameraPosition, Plane[] cameraPlanes)
        {
            if (ProfileReference == null)
                return true;

            Bounds aabb = GetAABB();

            // Volume is past maximum distance
            if ((cameraPosition - aabb.ClosestPoint(cameraPosition)).sqrMagnitude > maxDistance * maxDistance)
                return true;

            // Volume is outside camera frustum
            if (!GeometryUtility.TestPlanesAABB(cameraPlanes, aabb))
                return true;

            return false;
        }


        /// <returns>The local-space culling bounds of the volume</returns>
        public Bounds GetBounds()
        {
            if (volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder) 
                return BoundsUtility.capsuleBounds;

            // Default to cube
            return BoundsUtility.cubeBounds;
        }


        /// <returns>The world-space Axis-Aligned Bounds of the volume</returns>
        public Bounds GetAABB()
        {
            if (volumeType == VolumeType.Capsule || volumeType == VolumeType.Cylinder) 
                return GeometryUtility.CalculateBounds(BoundsUtility.capsuleCorners, transform.localToWorldMatrix);

            // Default to cube
            return GeometryUtility.CalculateBounds(BoundsUtility.cubeCorners, transform.localToWorldMatrix);
        }
    }
}
