using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using System.Collections.Generic;
using Unity.Collections;
using System.Reflection;


public partial class VolumetricLightPass
{
    private static readonly GlobalKeyword noiseKeyword = GlobalKeyword.Create("NOISE");
    private static readonly FieldInfo shadowCasterField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);


    private bool LightIsVisible(ref VisibleLight visibleLight, Camera camera) 
	{
        if (visibleLight.lightType == LightType.Directional)
            return true;

        Light light = visibleLight.light;
        Vector3 position = light.transform.position;

		if (!CullingUtility.CullSphere(light.transform.position, light.range))
            return false;

        if ((camera.transform.position - position).magnitude - light.range > feature.lightRange)
            return false;

		return true;
	}


    private List<SortedLight> GetSortedLights(ref RenderingData renderingData)
    {
        var shadowCasterPass = (AdditionalLightsShadowCasterPass)shadowCasterField.GetValue(renderingData.cameraData.renderer);

        LightData lightData = renderingData.lightData;
        NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

        List<SortedLight> sortedLights = new();

        CullingUtility.InitCullingPlanes(renderingData.cameraData.camera);

        for (int i = 0; i < visibleLights.Length; i++)
        {
            var visibleLight = visibleLights[i];

            if (!LightIsVisible(ref visibleLight, renderingData.cameraData.camera))
                continue;
            
            if (!visibleLight.light.TryGetComponent(out VolumetricLight volumeLight))
                continue;

            SortedLight light = new()
            {
                visibleLight = visibleLight,
                volumeLight = volumeLight,
                index = i == lightData.mainLightIndex ? -1 : shadowCasterPass.GetShadowLightIndexFromLightIndex(i),
            };

            UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, i, out light.position, out light.color, out light.attenuation, out light.spotDirection, out _);

            sortedLights.Add(light);
        }

        return sortedLights;
    }


    private void DrawLights(List<SortedLight> lights)
    {
        commandBuffer.SetGlobalVector("_LightRange", new Vector2(feature.lightRange - feature.falloffRange, feature.lightRange));
        commandBuffer.SetGlobalTexture("_DitherTexture", ditherTexture);

        commandBuffer.SetKeyword(noiseKeyword, feature.noise);
        commandBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);
        commandBuffer.SetGlobalVector("_NoiseVelocity", feature.noiseVelocity * feature.noiseScale);
        commandBuffer.SetGlobalVector("_NoiseData", new Vector4(feature.noiseScale, feature.noiseIntensity, feature.noiseIntensityOffset));


        commandBuffer.SetRenderTarget(VolumeLightBuffer);
        commandBuffer.ClearRenderTarget(true, true, Color.black);

        // Light loop
        for (int i = 0; i < lights.Count; i++)
            lights[i].volumeLight.RenderLight(commandBuffer, lightMaterial, lights[i]); 
    }


    private void BlendLights(RTHandle target)
    {
        commandBuffer.GetTemporaryRT(tempId, target.rt.descriptor);
        commandBuffer.Blit(target, tempHandle);

        commandBuffer.SetGlobalTexture("_BlitSource", tempHandle);
        commandBuffer.SetGlobalTexture("_BlitAdd", volumeLightTexture);

        // Use blit add kernel to merge target color and the light buffer
        commandBuffer.Blit(null, target, lightMaterial, 3);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }
}
