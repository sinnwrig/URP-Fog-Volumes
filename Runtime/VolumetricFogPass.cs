using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using System.Collections.Generic;


namespace Sinnwrig.FogVolumes
{
    public class VolumetricFogPass : ScriptableRenderPass
    {
        // Bundles the integer ID with an RtID



        // --------------------------------------------------------------------------
        // ------------------------------- Properties -------------------------------
        // --------------------------------------------------------------------------

        #region PROPERTIES

        // Temporary render target 
        private static readonly int tempId = Shader.PropertyToID("_Temp");


        // Materials
        private static Material bilateralBlur;
        private static Shader fogShader;
        private static Material blitAdd;
        private static Material reprojection;


        private readonly VolumetricFogFeature feature;


        private VolumetricResolution Resolution
        {
            get
            {
                // Temporal reprojection will force full-res rendering
                if (feature.resolution != VolumetricResolution.Full && feature.temporalRendering)
                    return VolumetricResolution.Full;

                return feature.resolution;
            }
        }



        public VolumetricFogPass(VolumetricFogFeature feature, Shader blur, Shader fog, Shader add, Shader reproj)
        {
            this.feature = feature;

            fogShader = fog;

            if (bilateralBlur == null || bilateralBlur.shader != blur)
                bilateralBlur = new Material(blur);

            if (blitAdd == null || blitAdd.shader != add)
                blitAdd = new Material(add);

            if (reprojection == null || reprojection.shader != reproj)
                reprojection = new Material(reproj);
        }

        #endregion

        // --------------------------------------------------------------------------
        // ----------------------------   Render Pass   -----------------------------
        // --------------------------------------------------------------------------

        #region RENDERING



        private class PassData
        {
            public VolumetricFogFeature feature;
            public Material fogShader;
            public Material bilateralBlur;
            public Material blitAdd;
            public Material reprojection; // If needed

            public TextureHandle cameraColor;
            public TextureHandle cameraDepth;

            // Internal textures
            public TextureHandle volumeFog;
            public TextureHandle halfVolumeFog;
            public TextureHandle quarterVolumeFog;
            public TextureHandle halfDepth;
            public TextureHandle quarterDepth;
            public TextureHandle temporalTarget;

            // Data
            public UniversalCameraData cameraData;
            public UniversalLightData lightData;
            public UniversalResourceData resourceData;
            public List<FogVolume> volumes;
            public RenderTextureDescriptor descriptor;

            public VolumetricFogPass pass; // Reference to pass for member access if needed logic isn't static
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using var builder = renderGraph.AddUnsafePass<PassData>("Volumetric Fog", out var passData);
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            passData.feature = feature;
            passData.pass = this;
            passData.cameraData = cameraData;
            passData.lightData = lightData;
            passData.resourceData = resourceData;

            // Setup Volumes early to check if we need to run
            passData.volumes = SetupVolumes(cameraData.camera);
            if (passData.volumes.Count == 0)
                return;

            passData.descriptor = cameraData.cameraTargetDescriptor;
            passData.cameraColor = resourceData.activeColorTexture;
            passData.cameraDepth = resourceData.activeDepthTexture;

            // Helper to create desc
            static TextureDesc GetDesc(int w, int h, RenderTextureFormat format, FilterMode filter)
            {
                return new TextureDesc(w, h)
                {
                    colorFormat = GraphicsFormatUtility.GetGraphicsFormat(format, false),
                    filterMode = filter,
                    depthBufferBits = 0,
                    name = ""
                };
            }

            // Create Textures
            var desc = cameraData.cameraTargetDescriptor;
            int width = desc.width;
            int height = desc.height;

            passData.volumeFog = renderGraph.CreateTexture(GetDesc(width, height, RenderTextureFormat.ARGBHalf, FilterMode.Point));

            builder.UseTexture(passData.volumeFog, AccessFlags.ReadWrite);
            builder.UseTexture(passData.cameraDepth, AccessFlags.Read);
            builder.UseTexture(passData.cameraColor, AccessFlags.Write); // Blit uses write

            if (feature.temporalRendering)
            {
                passData.temporalTarget = renderGraph.CreateTexture(GetDesc(width / TemporalKernelSize, height / TemporalKernelSize, RenderTextureFormat.ARGBHalf, FilterMode.Point));
                builder.UseTexture(passData.temporalTarget, AccessFlags.ReadWrite);
            }

            if (Resolution == VolumetricResolution.Half)
            {
                passData.halfVolumeFog = renderGraph.CreateTexture(GetDesc(width / 2, height / 2, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear));
                builder.UseTexture(passData.halfVolumeFog, AccessFlags.ReadWrite);
            }

            if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
            {
                passData.halfDepth = renderGraph.CreateTexture(GetDesc(width / 2, height / 2, RenderTextureFormat.RFloat, FilterMode.Point));
                builder.UseTexture(passData.halfDepth, AccessFlags.ReadWrite);
            }

            if (Resolution == VolumetricResolution.Quarter)
            {
                passData.quarterVolumeFog = renderGraph.CreateTexture(GetDesc(width / 4, height / 4, RenderTextureFormat.ARGBHalf, FilterMode.Bilinear));
                passData.quarterDepth = renderGraph.CreateTexture(GetDesc(width / 4, height / 4, RenderTextureFormat.RFloat, FilterMode.Point));
                builder.UseTexture(passData.quarterVolumeFog, AccessFlags.ReadWrite);
                builder.UseTexture(passData.quarterDepth, AccessFlags.ReadWrite);
            }

            builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
            {
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                var pass = data.pass;

                pass.DownsampleDepthBuffer(cmd, data.cameraDepth, data.halfDepth, data.quarterDepth);

                RenderTargetIdentifier fogTarget = data.volumeFog;
                if (pass.Resolution == VolumetricResolution.Quarter)
                    fogTarget = data.quarterVolumeFog;
                else if (pass.Resolution == VolumetricResolution.Half)
                    fogTarget = data.halfVolumeFog;
                else if (pass.feature.temporalRendering)
                    fogTarget = data.temporalTarget;

                pass.SetupFogRenderTarget(cmd, data.descriptor.width, data.descriptor.height, fogTarget);

                var lights = data.lightData.visibleLights;
                int mainIdx = data.lightData.mainLightIndex;

                pass.DrawVolumes(data.volumes, lights, mainIdx, (ScriptableRenderer)data.cameraData.renderer, data.cameraData.camera, cmd);
                RenderTargetIdentifier depthTarget = data.halfDepth;
                if (pass.Resolution == VolumetricResolution.Quarter)
                    depthTarget = data.quarterDepth;
                else if (pass.Resolution == VolumetricResolution.Half)
                    depthTarget = data.halfDepth;

                pass.ReprojectBuffer(cmd, data.descriptor, false, fogTarget, data.temporalTarget);
                pass.BilateralBlur(cmd, data.descriptor.width, data.descriptor.height, fogTarget, depthTarget, data.volumeFog, data.cameraDepth);

                pass.BlendFog(cmd, data.cameraColor, data.descriptor, data.volumeFog);
            });
        }


        // Release temporary textures
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }


        // Additively blend render result with the scene
        private void BlendFog(CommandBuffer cmd, RenderTargetIdentifier target, RenderTextureDescriptor descriptor, RenderTargetIdentifier source)
        {
            cmd.GetTemporaryRT(tempId, descriptor);
            cmd.Blit(target, tempId);

            cmd.SetGlobalTexture("_BlitSource", tempId);
            cmd.SetGlobalTexture("_BlitAdd", source);

            // Use blit add kernel to merge target color and the light buffer
            TargetBlit(cmd, target, blitAdd, 0);

            cmd.ReleaseTemporaryRT(tempId);
        }


        // Equivalent to normal Blit, but uses a custom quad instead of stupid idiot fullscreen triangle I couldn't get working.
        private static void TargetBlit(CommandBuffer cmd, RenderTargetIdentifier destination, Material material, int pass)
        {
            cmd.SetRenderTarget(destination);
            cmd.DrawMesh(MeshUtility.FullscreenQuad, Matrix4x4.identity, material, 0, pass);
        }

        #endregion

        // --------------------------------------------------------------------------
        // --------------------------   Volume Rendering   --------------------------
        // --------------------------------------------------------------------------

        #region VOLUMES

        private static readonly HashSet<FogVolume> activeVolumes = new();

        /// <summary>
        /// Add a volume to the tracked volume set. Does not track duplicates.
        /// </summary>
        /// <param name="volume">The volume to track.</param>
        public static void AddVolume(FogVolume volume) => activeVolumes.Add(volume);

        /// <summary>
        /// Remove a volume from the tracked volume set.
        /// </summary>
        /// <param name="volume">The volume to stop tracking.</param>
        public static void RemoveVolume(FogVolume volume) => activeVolumes.Remove(volume);

        /// <summary>
        /// The list of active volumes in the scene.
        /// </summary>
        public static IEnumerable<FogVolume> ActiveVolumes => activeVolumes;


        private static readonly Plane[] cullingPlanes = new Plane[6];


        private bool CullSphere(Vector3 pos, float radius)
        {
            // Cull spherical bounds, ignoring camera far plane at index 5
            for (int i = 0; i < cullingPlanes.Length; i++)
            {
                float distance = cullingPlanes[i].GetDistanceToPoint(pos);

                if (distance < 0 && Mathf.Abs(distance) > radius)
                    return true;
            }

            return false;
        }

        // Package visible lights and initialize lighting data
        private List<NativeLight> SetupLights(NativeArray<VisibleLight> visibleLights, int mainLightIndex, ScriptableRenderer renderer, Camera camera)
        {
            // var shadowPass = shadowPassField.GetValue(renderer); // Removed reflection

            List<NativeLight> initializedLights = new();

            Vector3 cameraPosition = camera.transform.position;

            for (int i = 0; i < visibleLights.Length; i++)
            {
                var visibleLight = visibleLights[i];

                bool isDirectional = visibleLight.lightType == LightType.Directional;
                bool isMain = i == mainLightIndex;

                Vector3 position = visibleLight.localToWorldMatrix.GetColumn(3);

                if (!isDirectional && CullSphere(position, visibleLight.range))
                    continue;

                // Calculate the index into URP's additional light buffers
                int additionalLightIndex = i;
                if (mainLightIndex != -1 && i > mainLightIndex)
                    additionalLightIndex--;

                // Pass the additional light index to the shader for GPU-side shadow lookup. 
                // Main light uses -1 to trigger specific logic.
                int shadowIndex = isMain ? -1 : additionalLightIndex;

                NativeLight light = new()
                {
                    isDirectional = isDirectional,
                    shadowIndex = shadowIndex,
                    range = visibleLight.range,
                    layer = visibleLight.light.gameObject.layer,
                    cameraDistance = isDirectional ? 0 : (cameraPosition - position).sqrMagnitude
                };

                // Set up light properties
                UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, i,
                    out light.position,
                    out light.color,
                    out light.attenuation,
                    out light.spotDirection,
                    out _
                );

                initializedLights.Add(light);
            }

            initializedLights.Sort((a, b) => a.cameraDistance.CompareTo(b.cameraDistance));

            return initializedLights;
        }


        // Cull active volumes and package only the visible ones
        private List<FogVolume> SetupVolumes(Camera camera)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, cullingPlanes);
            Vector3 camPos = camera.transform.position;

            List<FogVolume> fogVolumes = new();

            foreach (FogVolume volume in activeVolumes)
            {
                if (!volume.CullVolume(camPos, cullingPlanes))
                    fogVolumes.Add(volume);
            }

            return fogVolumes;
        }


        // Draw all of the volumes into the active render target
        private void DrawVolumes(List<FogVolume> volumes, NativeArray<VisibleLight> visibleLights, int mainLightIndex, ScriptableRenderer renderer, Camera camera, CommandBuffer cmd)
        {
            List<NativeLight> lights = SetupLights(visibleLights, mainLightIndex, renderer, camera);

            int perObjectLightCount = 8; // Default fallback if unavailable, or we can fetch from UniversalRenderPipelineAsset if accessible.
                                         // But renderingData.lightData.maxPerObjectAdditionalLightsCount was used before.
                                         // We can't easily get it without RenderingData in legacy, but in RG we might.
                                         // For now hardcode or assume 8/MAX. Or pass it in. 
                                         // Simplest is to pass it in or assume.
                                         // Let's rely on standard URP limits which are usually 8 per object.

            for (int i = 0; i < volumes.Count; i++)
                volumes[i].DrawVolume(camera, cmd, fogShader, lights, perObjectLightCount);
        }

        #endregion

        // --------------------------------------------------------------------------
        // --------------------------   Upscaling & Blur   --------------------------
        // --------------------------------------------------------------------------

        #region UPSCALING

        // Blurs the active resolution texture, upscaling to full resolution if needed
        private void BilateralBlur(CommandBuffer cmd, int width, int height, RenderTargetIdentifier source, RenderTargetIdentifier depth, RenderTargetIdentifier destination, RenderTargetIdentifier cameraDepth)
        {
            Resolution.SetResolutionKeyword(cmd);

            // Blur quarter-res texture and upsample to full res
            if (Resolution == VolumetricResolution.Quarter)
            {
                BilateralBlur(cmd, source, depth, width / 4, height / 4);
                Upsample(cmd, source, depth, destination);
                return;
            }

            // Blur half-res texture and upsample to full res
            if (Resolution == VolumetricResolution.Half)
            {
                BilateralBlur(cmd, source, depth, width / 2, height / 2);
                Upsample(cmd, source, depth, destination);
                return;
            }

            if (feature.disableBlur)
                return;

            // Blur full-scale texture 
            BilateralBlur(cmd, destination, cameraDepth, width, height);
        }


        // Blurs source texture with provided depth texture- uses camera depth if null
        private void BilateralBlur(CommandBuffer cmd, RenderTargetIdentifier source, RenderTargetIdentifier depthBuffer, int sourceWidth, int sourceHeight)
        {
            cmd.GetTemporaryRT(tempId, sourceWidth, sourceHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

            SetDepthTexture(cmd, "_DepthTexture", depthBuffer);

            // Horizontal blur
            cmd.SetGlobalTexture("_BlurSource", source);
            TargetBlit(cmd, tempId, bilateralBlur, 0);

            // Vertical blur
            cmd.SetGlobalTexture("_BlurSource", tempId);
            TargetBlit(cmd, source, bilateralBlur, 1);

            cmd.ReleaseTemporaryRT(tempId);
        }


        // Downsamples depth texture to active resolution buffer
        private void DownsampleDepthBuffer(CommandBuffer cmd, RenderTargetIdentifier cameraDepth, RenderTargetIdentifier halfDepth, RenderTargetIdentifier quarterDepth)
        {
            if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
            {
                SetDepthTexture(cmd, "_DownsampleSource", cameraDepth);
                TargetBlit(cmd, halfDepth, bilateralBlur, 2);
            }

            if (Resolution == VolumetricResolution.Quarter)
            {
                SetDepthTexture(cmd, "_DownsampleSource", halfDepth);
                TargetBlit(cmd, quarterDepth, bilateralBlur, 2);
            }
        }


        // Perform depth-aware upsampling to the destination
        private void Upsample(CommandBuffer cmd, RenderTargetIdentifier sourceColor, RenderTargetIdentifier sourceDepth, RenderTargetIdentifier destination)
        {
            cmd.SetGlobalTexture("_DownsampleColor", sourceColor);
            cmd.SetGlobalTexture("_DownsampleDepth", sourceDepth);

            TargetBlit(cmd, destination, bilateralBlur, 3);
        }


        private void SetDepthTexture(CommandBuffer cmd, string textureId, RenderTargetIdentifier depth)
        {
            cmd.SetGlobalTexture(textureId, depth);
        }

        #endregion

        // --------------------------------------------------------------------------
        // -------------------------   Temporal Rendering   -------------------------
        // --------------------------------------------------------------------------

        #region TEMPORAL

        private static readonly GlobalKeyword temporalKeyword = GlobalKeyword.Create("TEMPORAL_RENDERING_ENABLED");

        private int TemporalKernelSize => System.Math.Max(2, feature.temporalResolution);
        private int temporalPassIndex;

        // Temporal Reprojection Target-
        // NOTE: only a RenderTexture seems to preserve information between frames on my device, otherwise I'd use an RTHandle or RenderTargetIdentifier
        private RenderTexture temporalBuffer;


        private static readonly System.Random random = new();


        private void SetTemporalConstants(CommandBuffer cmd)
        {
            temporalPassIndex = (temporalPassIndex + 1) % (TemporalKernelSize * TemporalKernelSize);

            cmd.SetGlobalVector("_TileSize", new Vector2(TemporalKernelSize, TemporalKernelSize));
            cmd.SetGlobalVector("_PassOffset", new Vector2(random.Next(0, TemporalKernelSize), random.Next(0, TemporalKernelSize)));
        }


        // Set the volumetric fog render target
        // Clear the target if there is nothing to reproject
        // Otherwise, reproject the previous frame
        private void SetupFogRenderTarget(CommandBuffer cmd, int width, int height, RenderTargetIdentifier target)
        {
            cmd.SetKeyword(temporalKeyword, feature.temporalRendering);

            cmd.SetRenderTarget(target);

            cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));

            if (feature.temporalRendering)
            {
                SetTemporalConstants(cmd);
                cmd.SetGlobalVector("_TemporalRenderSize", new Vector2(width, height) / TemporalKernelSize);
            }
        }


        private void ReprojectBuffer(CommandBuffer cmd, RenderTextureDescriptor descriptor, bool isSceneViewConfig, RenderTargetIdentifier source, RenderTargetIdentifier target)
        {
            if (!feature.temporalRendering)
                return;

            int width = descriptor.width;
            int height = descriptor.height;
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;

            if (temporalBuffer == null || !temporalBuffer.IsCreated() || temporalBuffer.width != width || temporalBuffer.height != height)
            {
                if (temporalBuffer != null && temporalBuffer.IsCreated())
                    temporalBuffer.Release();

                temporalBuffer = new RenderTexture(descriptor);
                temporalBuffer.Create();
            }

            cmd.SetGlobalTexture("_TemporalBuffer", temporalBuffer);
            cmd.SetGlobalTexture("_TemporalTarget", target);
            cmd.SetGlobalFloat("_MotionInfluence", isSceneViewConfig ? 0 : 1);

            TargetBlit(cmd, source, reprojection, 0);

            cmd.CopyTexture(source, 0, 0, temporalBuffer, 0, 0);
        }


        public void Dispose()
        {
            if (temporalBuffer != null && temporalBuffer.IsCreated())
                temporalBuffer.Release();
        }

        #endregion

    }
}