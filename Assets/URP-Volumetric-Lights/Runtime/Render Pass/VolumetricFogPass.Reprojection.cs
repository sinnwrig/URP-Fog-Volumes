using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public partial class VolumetricFogPass : ScriptableRenderPass
{
    private Matrix4x4 prevVMatrix;
    private Matrix4x4 prevVpMatrix;
    private Matrix4x4 prevInvVpMatrix;

    private int temporalPass;
    private bool hasReprojectionTexture = false;


    // Temporal Reprojection Target-
    // NOTE: only a RenderTexture seems to preserve information between frames on my device, otherwise I'd use an RTHandle or RenderTargetIdentifier
    private RenderTexture reprojectionBuffer;


    private void SetReprojectionMatrices(Camera cam)
    {
        Matrix4x4 vpMatrix = cam.worldToCameraMatrix * cam.projectionMatrix;
        Matrix4x4 invVpMatrix = vpMatrix.inverse;

        commandBuffer.SetGlobalMatrix("_PrevView", prevVMatrix);
        commandBuffer.SetGlobalMatrix("_PrevViewProjection", prevVpMatrix);
        commandBuffer.SetGlobalMatrix("_PrevInvViewProjection", prevInvVpMatrix);

        commandBuffer.SetGlobalMatrix("_CameraView", cam.worldToCameraMatrix);
        commandBuffer.SetGlobalMatrix("_CameraViewProjection", vpMatrix);
        commandBuffer.SetGlobalMatrix("_InverseViewProjection", invVpMatrix);

        prevVMatrix = cam.worldToCameraMatrix;
        prevVpMatrix = vpMatrix;
        prevInvVpMatrix = invVpMatrix;
    }   


    // Set the volumetric fog render target
    // Clear the target if there is nothing to reproject
    // Otherwise, reproject the previous frame
    private void SetVolumeFogTarget(Camera cam)
    {
        commandBuffer.SetKeyword(reprojectionKeyword, feature.enableReprojection);

        if (!feature.enableReprojection || !hasReprojectionTexture)
        {
            if (Resolution == VolumetricResolution.Quarter)
                commandBuffer.SetRenderTarget(quarterVolumeFogTexture);
            else if (Resolution == VolumetricResolution.Half)
                commandBuffer.SetRenderTarget(halfVolumeFogTexture);
            else
                commandBuffer.SetRenderTarget(volumeFogTexture);

            commandBuffer.ClearRenderTarget(true, true, Color.black);
            return;
        }

        Reproject(cam);
    }


    private void Reproject(Camera cam)
    {
        SetReprojectionMatrices(cam);

        temporalPass = (temporalPass + 1) % feature.temporalPassCount;
        commandBuffer.SetGlobalInt("_TemporalPassCount", feature.temporalPassCount);
        commandBuffer.SetGlobalInt("_TemporalPass", temporalPass);

        commandBuffer.SetGlobalTexture("_ReprojectSource", reprojectionBuffer);
        TargetBlit(commandBuffer, volumeFogTexture, fogMaterial, 2);
    }


    private void SetupReprojectionTexture(ref RenderingData data)
    {
        if (!feature.enableReprojection)
            return;

        RenderTextureDescriptor descriptor = data.cameraData.cameraTargetDescriptor;
        int width = descriptor.width;
        int height = descriptor.height;
        descriptor.colorFormat = RenderTextureFormat.ARGBHalf;

        if (reprojectionBuffer == null || !reprojectionBuffer.IsCreated() || reprojectionBuffer.width != width || reprojectionBuffer.height != height)
        {
            if (reprojectionBuffer != null && reprojectionBuffer.IsCreated())
                reprojectionBuffer.Release();

            reprojectionBuffer = new RenderTexture(descriptor);
            reprojectionBuffer.Create();
        }

        commandBuffer.CopyTexture(volumeFogTexture, 0, 0, reprojectionBuffer, 0, 0);

        hasReprojectionTexture = true;
    }


    public void Dispose()
    {
        if (reprojectionBuffer != null && reprojectionBuffer.IsCreated())
            reprojectionBuffer.Release();
    }
}
