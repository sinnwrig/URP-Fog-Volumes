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
        private struct VisibleLightRemap
        {
            public VisibleLight lightData;
            public int visibleLightIndex;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var universalRenderer = renderingData.cameraData.renderer as UniversalRenderer;
            var m_AdditionalLightsShadowCasterPassField = universalRenderer.GetType().GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);
            var m_AdditionalLightsShadowCasterPass = (AdditionalLightsShadowCasterPass)m_AdditionalLightsShadowCasterPassField.GetValue(universalRenderer);

            var visibleLights = renderingData.lightData.visibleLights;
            var visibleLightCount = visibleLights.Length;


            var corgiLightsToShadowIndex = new NativeArray<int>(visibleLightCount, Allocator.Temp);


            var visibleLightCopy = new List<VisibleLightRemap>(visibleLights.Length);


            for(var l = 0; l < visibleLights.Length; ++l)
            {
                visibleLightCopy.Add(new VisibleLightRemap()
                {
                    lightData = visibleLights[l],
                    visibleLightIndex = l
                });
            }


            var cameraPosition = renderingData.cameraData.camera.transform.position;
            visibleLightCopy.Sort((a, b) =>
            {
                var distance_a = Vector3.Distance(a.lightData.light.transform.position, cameraPosition);
                var distance_b = Vector3.Distance(b.lightData.light.transform.position, cameraPosition);
                return distance_a.CompareTo(distance_b);
            });


            for (int i = 0, lightIter = 0; i < visibleLightCopy.Count; ++i)
            {
                var lightIndex = visibleLightCopy[i].visibleLightIndex;

                if (renderingData.lightData.mainLightIndex != lightIndex)
                {
                    corgiLightsToShadowIndex[lightIter] = m_AdditionalLightsShadowCasterPass.GetShadowLightIndexFromLightIndex(lightIndex); 
                    lightIter++;
                }
            }
        }
    }
}