using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using System.Collections.Generic;
using Unity.Collections;
using System.Reflection;


public partial class VolumetricLightPass : ScriptableRenderPass
{
    private static readonly GlobalKeyword spotLight = GlobalKeyword.Create("SPOT_LIGHT");
    private static readonly GlobalKeyword pointLight = GlobalKeyword.Create("POINT_LIGHT");
    private static readonly GlobalKeyword directionalLight = GlobalKeyword.Create("DIRECTIONAL_LIGHT");
    private static readonly GlobalKeyword noiseKeyword = GlobalKeyword.Create("NOISE");


    private static readonly FieldInfo shadowCasterField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Plane[] frustumPlanes = new Plane[6];


    public struct SortedLight
    {
        public VisibleLight visibleLight;
        public int index;
        public VolumetricLight volumeLight;
        public Vector4 position;
        public Vector4 color;
        public Vector4 attenuation;
        public Vector4 spotDirection;
    }


    public VolumetricLightFeature feature;
    public VolumetricResolution Resolution => feature.resolution;


    private static Material bilateralBlur;
    private static Material lightMaterial;


    private static Texture3D noiseTexture;
    private static Texture2D ditherTexture;

    public CommandBuffer commandBuffer;
    


    public VolumetricLightPass(VolumetricLightFeature feature, Shader blur, Shader light)
    {
        this.feature = feature;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (lightMaterial == null || lightMaterial.shader != light)
            lightMaterial = new Material(light);

        if (noiseTexture == null)
            noiseTexture = Resources.Load("Noise3DTexture") as Texture3D;

        if (ditherTexture == null)
            ditherTexture = Resources.Load("DitherTex") as Texture2D;
    }   



    private bool LightIsVisible(ref VisibleLight visibleLight, Plane[] frustumPlanes, Camera camera) 
	{
        if (visibleLight.lightType == LightType.Directional)
            return true;

        Light light = visibleLight.light;
        Vector3 position = light.transform.position;

		// Cull light spheres with camera frustum
		for (int i = 0; i < frustumPlanes.Length; i++) 
		{
			float distance = frustumPlanes[i].GetDistanceToPoint(position);

			if (distance < 0 && Mathf.Abs(distance) > light.range) 
				return false;
		}

        float distanceToSphereBounds = (camera.transform.position - position).magnitude - light.range;

        // Cull faraway lights
        if (distanceToSphereBounds > feature.lightRange)
            return false;

		return true;
	}


    private List<SortedLight> GetSortedLights(ref RenderingData renderingData)
    {
        var shadowCasterPass = (AdditionalLightsShadowCasterPass)shadowCasterField.GetValue(renderingData.cameraData.renderer);

        LightData lightData = renderingData.lightData;
        NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

        List<SortedLight> sortedLights = new();

        GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera, frustumPlanes);
        
        for (int i = 0; i < visibleLights.Length; i++)
        {
            var visibleLight = visibleLights[i];

            if (!LightIsVisible(ref visibleLight, frustumPlanes, renderingData.cameraData.camera))
                continue;
            
            if (!visibleLight.light.TryGetComponent(out VolumetricLight volumeLight))
                continue;

            SortedLight light = new()
            {
                visibleLight = visibleLight,
                index = i == lightData.mainLightIndex ? -1 : shadowCasterPass.GetShadowLightIndexFromLightIndex(i),
                volumeLight = volumeLight,
            };


            // Why do we have to get light and shadow constants ourselves? Because URP lighting doesn't work reliably in a post-fx shader.
            UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, i, out light.position, out light.color, out light.attenuation, out light.spotDirection, out _);

            sortedLights.Add(light);
        }

        return sortedLights;
    }



    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var renderer = renderingData.cameraData.renderer;
        var descriptor = renderingData.cameraData.cameraTargetDescriptor;

        #if UNITY_2022_1_OR_NEWER
            var cameraColor = renderer.cameraColorTargetHandle;
            var cameraDepth = renderer.cameraDepthTargetHandle;
        #else
            var cameraColor = renderer.cameraColorTarget;
            var cameraDepth = renderer.cameraDepthTarget;
        #endif

        commandBuffer = CommandBufferPool.Get("Volumetric Light Pass");
        commandBuffer.Clear();

        var lights = GetSortedLights(ref renderingData);

        if (lights.Count == 0)
            return;

        DownsampleDepthBuffer();
        DrawLights(lights);
        BilateralBlur(descriptor.width, descriptor.height);
        BlendLights(cameraColor, descriptor);

        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    private void DrawLights(List<SortedLight> lights)
    {
        commandBuffer.GetTemporaryRT(tempId, lightBufferDescriptor, FilterMode.Point);

        var source = VolumeLightBuffer;
        var target = tempHandle;

        // Clear initial texture
        ClearColor(commandBuffer, target, Color.black);


        commandBuffer.SetGlobalVector("_LightRange", new Vector2(feature.lightRange - feature.falloffRange, feature.lightRange));
        commandBuffer.SetGlobalTexture("_DitherTexture", ditherTexture);


        commandBuffer.SetKeyword(noiseKeyword, feature.noise);
        commandBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);
        commandBuffer.SetGlobalVector("_NoiseVelocity", feature.noiseVelocity * feature.noiseScale);
        commandBuffer.SetGlobalVector("_NoiseData", new Vector4(feature.noiseScale, feature.noiseIntensity, feature.noiseIntensityOffset));

        // Light loop
        for (int i = 0; i < lights.Count; i++)
        {
            if (!lights[i].volumeLight.CanRender())
                continue;

            (source, target) = (target, source);
            DrawLight(lights[i], source, target);   
        }

        // Source ends up as the final texture after swap
        if (target == tempHandle)
            commandBuffer.Blit(target, VolumeLightBuffer);
        
        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private void DrawLight(SortedLight light, RenderTargetIdentifier source, RenderTargetIdentifier target)
    {
        VolumetricLight volumeLight = light.volumeLight;

        commandBuffer.SetGlobalInt("_SampleCount", volumeLight.sampleCount);

        commandBuffer.SetGlobalVector("_MieG", volumeLight.GetMie());
        commandBuffer.SetGlobalVector("_VolumetricLight", new Vector2(volumeLight.scatteringCoef, volumeLight.extinctionCoef));
        commandBuffer.SetGlobalFloat("_MaxRayLength", volumeLight.maxRayLength);
        commandBuffer.SetGlobalMatrix("_InvLightMatrix", volumeLight.GetLightMatrix());

        // Attenuation sampling params
        commandBuffer.SetGlobalInt("_LightIndex", light.index);
        commandBuffer.SetGlobalVector("_LightPosition", light.position);
        commandBuffer.SetGlobalVector("_LightColor", light.color);
        commandBuffer.SetGlobalVector("_LightAttenuation", light.attenuation);
        commandBuffer.SetGlobalVector("_SpotDirection", light.spotDirection);

        switch (volumeLight.Light.type)
        {
            case LightType.Spot:
                SetKeyword(spotLight, directionalLight, pointLight);
            break;

            case LightType.Point:
                SetKeyword(pointLight, directionalLight, spotLight);
            break;

            case LightType.Directional:
                SetKeyword(directionalLight, spotLight, pointLight);
            break;
        }
        
        commandBuffer.SetGlobalTexture("_SourceTexture", source); 
        commandBuffer.Blit(source, target, lightMaterial, 0);
    }


    private void BlendLights(RenderTargetIdentifier target, RenderTextureDescriptor targetDescriptor)
    {
        commandBuffer.GetTemporaryRT(tempId, targetDescriptor);
        commandBuffer.Blit(target, tempHandle);

        commandBuffer.SetGlobalTexture("_SourceTexture", tempHandle);
        commandBuffer.SetGlobalTexture("_SourceAdd", volumeLightTexture);

        // Use blit add kernel to merge target color and the light buffer
        commandBuffer.Blit(volumeLightTexture, target, lightMaterial, 1);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    private void SetKeyword(GlobalKeyword keyword, params GlobalKeyword[] other)
    {
        commandBuffer.EnableKeyword(keyword);

        for (int i = 0; i < other.Length; i++)
            commandBuffer.DisableKeyword(other[i]);
    }


    private void ClearColor(CommandBuffer cmd, RenderTargetIdentifier rt, Color color)
    {
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(RTClearFlags.Color, color, 1, 0);
    }
}
