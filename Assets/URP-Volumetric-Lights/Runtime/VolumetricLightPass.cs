using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using System.Collections.Generic;
using Unity.Collections;
using System.Reflection;


public partial class VolumetricLightPass : ScriptableRenderPass
{
    private static readonly GlobalKeyword noiseKeyword = GlobalKeyword.Create("NOISE");

    // Blur keywords
    private static readonly GlobalKeyword fullResKernel = GlobalKeyword.Create("FULL_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword halfResKernel = GlobalKeyword.Create("HALF_RES_BLUR_KERNEL_SIZE");
    private static readonly GlobalKeyword quarterResKernel = GlobalKeyword.Create("QUARTER_RES_BLUR_KERNEL_SIZE");

    private static readonly GlobalKeyword fullDepthSource = GlobalKeyword.Create("SOURCE_FULL_DEPTH");


    // Depth render targets
    private static readonly int halfDepthId = Shader.PropertyToID("_HalfDepthTarget");
    private static readonly RenderTargetIdentifier halfDepthTarget = new(halfDepthId);
    private static readonly int quarterDepthId = Shader.PropertyToID("_QuarterDepthTarget");
    private static readonly RenderTargetIdentifier quarterDepthTarget = new(quarterDepthId);

    // Light render targets
    private static readonly int volumeLightId = Shader.PropertyToID("_VolumeLightTexture");
    private static readonly RenderTargetIdentifier volumeLightTexture = new(volumeLightId);
    private static readonly int halfVolumeLightId = Shader.PropertyToID("_HalfVolumeLightTexture");
    private static readonly RenderTargetIdentifier halfVolumeLightTexture = new(halfVolumeLightId);
    private static readonly int quarterVolumeLightId = Shader.PropertyToID("_QuarterVolumeLightTexture");
    private static readonly RenderTargetIdentifier quarterVolumeLightTexture = new(quarterVolumeLightId);

    // Temp render target 
    private static readonly int tempId = Shader.PropertyToID("_Temp");
    private RenderTargetIdentifier tempHandle = new(tempId);



    private static readonly FieldInfo shadowCasterField = typeof(UniversalRenderer).GetField("m_AdditionalLightsShadowCasterPass", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Plane[] cullingPlanes = new Plane[6];


    private static Material bilateralBlur;
    private static Material lightMaterial;

    private static Texture3D noiseTexture;


    public VolumetricLightFeature feature;
    public VolumetricResolution Resolution => feature.resolution;

    public CommandBuffer commandBuffer;


    // Active resolution target
    public RenderTargetIdentifier VolumeLightBuffer 
    {
        get 
        {
            return Resolution switch
            {
                VolumetricResolution.Quarter => quarterVolumeLightTexture,
                VolumetricResolution.Half => halfVolumeLightTexture,
                VolumetricResolution.Full => volumeLightTexture,
                _ => volumeLightTexture,
            };
        }
    }
    


    public VolumetricLightPass(VolumetricLightFeature feature, Shader blur, Shader light)
    {
        this.feature = feature;

        if (bilateralBlur == null || bilateralBlur.shader != blur)
            bilateralBlur = new Material(blur);

        if (lightMaterial == null || lightMaterial.shader != light)
            lightMaterial = new Material(light);

        if (noiseTexture == null)
            noiseTexture = Resources.Load("Noise3DTexture") as Texture3D;
    }   


    // Allocate temporary textures
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
    {
        RenderTextureDescriptor descriptor = data.cameraData.cameraTargetDescriptor;

        int width = descriptor.width;
        int height = descriptor.height;
        var colorFormat = RenderTextureFormat.ARGBHalf;
        var depthFormat = RenderTextureFormat.RFloat;

        cmd.GetTemporaryRT(volumeLightId, width, height, 0, FilterMode.Point, colorFormat);

        if (Resolution == VolumetricResolution.Half)
            cmd.GetTemporaryRT(halfVolumeLightId, width / 2, height / 2, 0, FilterMode.Bilinear, colorFormat);

        // Half/Quarter res both need half-res depth buffer for downsampling
        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
            cmd.GetTemporaryRT(halfDepthId, width / 2, height / 2, 0, FilterMode.Point, depthFormat);

        if (Resolution == VolumetricResolution.Quarter)
        {
            cmd.GetTemporaryRT(quarterVolumeLightId, width / 4, height / 4, 0, FilterMode.Bilinear, colorFormat);
            cmd.GetTemporaryRT(quarterDepthId, width / 4, height / 4, 0, FilterMode.Point, depthFormat);
        }
    }


    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var lights = GetSortedLights(ref renderingData);
        if (lights.Count == 0)
            return;

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

        DownsampleDepthBuffer();
        DrawLights(lights);
        BilateralBlur(descriptor.width, descriptor.height);
        BlendLights(cameraColor);

        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    // Release temporary textures
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(volumeLightId);

        if (Resolution == VolumetricResolution.Half)
            cmd.ReleaseTemporaryRT(halfVolumeLightId);

        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
            cmd.ReleaseTemporaryRT(halfDepthId);

        if (Resolution == VolumetricResolution.Quarter)
        {
            cmd.ReleaseTemporaryRT(quarterVolumeLightId);
            cmd.ReleaseTemporaryRT(quarterDepthId);
        }
    }



    // Perform frustum culling and distance culling on visible light
    private bool LightIsVisible(ref VisibleLight visibleLight, Camera camera) 
	{
        if (visibleLight.lightType == LightType.Directional)
            return true;

        Light light = visibleLight.light;
        Vector3 viewPosition = camera.transform.position;
        Vector3 position = light.transform.position;

        for (int i = 0; i < cullingPlanes.Length; i++) 
		{
			float distance = cullingPlanes[i].GetDistanceToPoint(position);

			if (distance < 0 && Mathf.Abs(distance) > light.range) 
				return false;
		}

        if ((viewPosition - position).magnitude - light.range > feature.lightRange)
            return false;

		return true;
	}


    private List<SortedLight> GetSortedLights(ref RenderingData renderingData)
    {
        var shadowCasterPass = (AdditionalLightsShadowCasterPass)shadowCasterField.GetValue(renderingData.cameraData.renderer);

        LightData lightData = renderingData.lightData;
        NativeArray<VisibleLight> visibleLights = lightData.visibleLights;

        List<SortedLight> sortedLights = new();
        
        GeometryUtility.CalculateFrustumPlanes(renderingData.cameraData.camera, cullingPlanes);

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
                index = i == lightData.mainLightIndex ? -1 : shadowCasterPass.GetShadowLightIndexFromLightIndex(i), // We should not need to access a private field to get shadow index, but we must. 
            };

            UniversalRenderPipeline.InitializeLightConstants_Common(visibleLights, i, out light.position, out light.color, out light.attenuation, out light.spotDirection, out _);

            sortedLights.Add(light);
        }

        return sortedLights;
    }


    private void DrawLights(List<SortedLight> lights)
    {
        commandBuffer.SetGlobalVector("_LightRange", new Vector2(feature.lightRange - feature.falloffRange, feature.lightRange));

        commandBuffer.SetKeyword(noiseKeyword, feature.noise);

        float dirLightIntensity = 1.0f;
        float lightIntensity = 1.0f;

        Vector3 noiseVelocity = Vector3.zero;
        float noiseIntensity = 1.0f;
        float intensityOffset = 0.25f;

        if (VolumeManager.instance != null && VolumeManager.instance.stack != null)
        {
            var volumeSettings = VolumeManager.instance.stack.GetComponent<LightVolume>();

            if (volumeSettings != null)
            {
                dirLightIntensity = volumeSettings.directionalIntensityModifier.value;
                lightIntensity = volumeSettings.intensityModifier.value;

                noiseVelocity = volumeSettings.noiseDirection.value;
                noiseIntensity = volumeSettings.noiseIntensity.value;
                intensityOffset = volumeSettings.noiseIntensityOffset.value;
            }
        }


        commandBuffer.SetKeyword(noiseKeyword, feature.noise);
        commandBuffer.SetGlobalTexture("_NoiseTexture", noiseTexture);
        commandBuffer.SetGlobalVector("_NoiseVelocity", noiseVelocity * feature.noiseScale);
        commandBuffer.SetGlobalVector("_NoiseData", new Vector4(feature.noiseScale, noiseIntensity, intensityOffset));

        commandBuffer.SetRenderTarget(VolumeLightBuffer);
        commandBuffer.ClearRenderTarget(true, true, Color.black);

        // Where the magic loop happens
        for (int i = 0; i < lights.Count; i++)
        {
            VolumetricLight light = lights[i].volumeLight;
            light.RenderLight(commandBuffer, lightMaterial, lights[i], light.Light.type == LightType.Directional ? dirLightIntensity : lightIntensity);
        }
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



    // Blurs the active resolution texture, then upscales and blits (if neccesary) to full resolution texture
    private void BilateralBlur(int width, int height)
    {
        if (Resolution == VolumetricResolution.Quarter)
        {
            commandBuffer.EnableKeyword(quarterResKernel);
            commandBuffer.DisableKeyword(fullResKernel);
            commandBuffer.DisableKeyword(halfResKernel);
            
            BilateralBlur(quarterVolumeLightTexture, quarterDepthTarget, width / 4, height / 4); 
            
            // Upsample to full res
            Upsample(quarterVolumeLightTexture, quarterDepthTarget, volumeLightTexture);
        }
        else if (Resolution == VolumetricResolution.Half)
        {
            commandBuffer.EnableKeyword(halfResKernel);
            commandBuffer.DisableKeyword(fullResKernel);
            commandBuffer.DisableKeyword(quarterResKernel);

            BilateralBlur(halfVolumeLightTexture, halfDepthTarget, width / 2, height / 2);

            // Upsample to full res
            Upsample(halfVolumeLightTexture, halfDepthTarget, volumeLightTexture);
        }
        else
        {
            commandBuffer.EnableKeyword(fullResKernel);
            commandBuffer.DisableKeyword(halfResKernel);
            commandBuffer.DisableKeyword(quarterResKernel);

            // Blur full-scale texture- use full-scale depth texture from shader
            BilateralBlur(volumeLightTexture, null, width, height);
        }
    }


    // Blurs source texture with provided depth texture to preserve edges- uses camera depth texture if none is provided
    private void BilateralBlur(RenderTargetIdentifier source, RenderTargetIdentifier? depthBuffer, int sourceWidth, int sourceHeight)
    {
        commandBuffer.GetTemporaryRT(tempId, sourceWidth, sourceHeight, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);

        SetDepthTexture("_DepthTexture", depthBuffer);

        // Horizontal blur
        commandBuffer.SetGlobalTexture("_BlurSource", source);
        commandBuffer.Blit(null, tempHandle, bilateralBlur, 0);

        // Vertical blur
        commandBuffer.SetGlobalTexture("_BlurSource", tempHandle);
        commandBuffer.Blit(null, source, bilateralBlur, 1);

        commandBuffer.ReleaseTemporaryRT(tempId);
    }


    // Downsamples depth texture to active resolution buffer
    private void DownsampleDepthBuffer()
    {
        if (Resolution == VolumetricResolution.Half || Resolution == VolumetricResolution.Quarter)
        {
            SetDepthTexture("_DownsampleSource", null);
            commandBuffer.Blit(null, halfDepthTarget, bilateralBlur, 2);
        }

        if (Resolution == VolumetricResolution.Quarter)
        {
            SetDepthTexture("_DownsampleSource", halfDepthTarget);
            commandBuffer.Blit(null, quarterDepthTarget, bilateralBlur, 2);
        }
    }


    private void Upsample(RenderTargetIdentifier sourceColor, RenderTargetIdentifier sourceDepth, RenderTargetIdentifier destination)
    {
        commandBuffer.SetGlobalTexture("_DownsampleColor", sourceColor);
        commandBuffer.SetGlobalTexture("_DownsampleDepth", sourceDepth);

        commandBuffer.Blit(null, destination, bilateralBlur, 3);
    }


    // Use shader variants to either 
    // 1: Use the depth texture being assigned 
    // 2: Use the _CameraDepthTexture property
    private void SetDepthTexture(string textureId, RenderTargetIdentifier? depth)
    {
        commandBuffer.SetKeyword(fullDepthSource, !depth.HasValue);

        if (depth.HasValue)
            commandBuffer.SetGlobalTexture(textureId, depth.Value);
    }

}
