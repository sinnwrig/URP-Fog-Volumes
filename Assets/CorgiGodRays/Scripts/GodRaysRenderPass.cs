namespace CorgiGodRays
{
    using System.Collections.Generic;
    using System.Reflection;
    using Unity.Collections;
    using UnityEngine;
    using UnityEngine.Rendering;
    using UnityEngine.Rendering.Universal;
    using UnityEngine.Rendering.Universal.Internal;

    public class GodRaysRenderPass : ScriptableRenderPass
    {

        void InitializeLightConstants(NativeArray<VisibleLight> lights, 
            int lightIndex, 
            out Vector4 lightPos, 
            out Vector4 lightColor, 
            out Vector4 lightAttenuation, 
            out Vector4 lightSpotDir, 
            out Vector4 lightOcclusionProbeChannel)
        {
            UniversalRenderPipeline.InitializeLightConstants_Common(lights, 
                lightIndex, 
                out lightPos, 
                out lightColor, 
                out lightAttenuation, 
                out lightSpotDir, 
                out lightOcclusionProbeChannel);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var universalRenderer = renderingData.cameraData.renderer as UniversalRenderer;

            // handle custom light stuff 
            var m_AdditionalLightsShadowCasterPassField = universalRenderer.GetType().GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);
            var m_AdditionalLightsShadowCasterPass = (AdditionalLightsShadowCasterPass)m_AdditionalLightsShadowCasterPassField.GetValue(universalRenderer);

            var visibleLights = renderingData.lightData.visibleLights;
            var visibleLightCount = visibleLights.Length;
            var corgiLightCount = 0;

            var additionalLightsData = new NativeArray<ShaderInput.LightData>(visibleLightCount, Allocator.Temp);
            var corgiLightsToShadowIndex = new NativeArray<int>(visibleLightCount, Allocator.Temp);

            var maxLightCount = 256;

            var visibleLightCopy = new List<(VisibleLight, int)>(renderingData.lightData.visibleLights.Length);

            for(var l = 0; l < renderingData.lightData.visibleLights.Length; ++l)
            {
                visibleLightCopy.Add((renderingData.lightData.visibleLights[l], l));
            }

            var cameraPosition = renderingData.cameraData.camera.transform.position;
            visibleLightCopy.Sort((a, b) =>
            {
                var distance_a = Vector3.Distance(a.Item1.light.transform.position, cameraPosition);
                var distance_b = Vector3.Distance(b.Item1.light.transform.position, cameraPosition);
                return distance_a.CompareTo(distance_b);
            });
            

            for (int i = 0, lightIter = 0; i < visibleLightCopy.Count && lightIter < maxLightCount; ++i)
            {
                var lightRemap = visibleLightCopy[i];
                var lightIndex = lightRemap.Item2;

                if (renderingData.lightData.mainLightIndex != lightIndex)
                {
                    ShaderInput.LightData data = default;

                    InitializeLightConstants(visibleLights, 
                        lightIndex,
                        out data.position, 
                        out data.color, 
                        out data.attenuation,
                        out data.spotDirection, 
                        out data.occlusionProbeChannels);

                    additionalLightsData[lightIter] = data;
                    corgiLightsToShadowIndex[lightIter] = m_AdditionalLightsShadowCasterPass.GetShadowLightIndexFromLightIndex(lightIndex); 
                    lightIter++;
                    corgiLightCount++;
                }

                additionalLightsData.Dispose();
                corgiLightsToShadowIndex.Dispose();
            }
        }
    }
}