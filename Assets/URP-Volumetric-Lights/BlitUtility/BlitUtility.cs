using UnityEngine;
using UnityEngine.Rendering;


public static class BlitUtility 
{
    static readonly int currentBlitTarget = Shader.PropertyToID("_BlitTarget");
    
    
    static readonly int blitTargetA = Shader.PropertyToID("_BlitA");
    static readonly int blitTargetB = Shader.PropertyToID("_BlitB");


    static RenderTargetIdentifier destinationA = new(blitTargetA);
    static RenderTargetIdentifier destinationB = new(blitTargetB);


    static RenderTargetIdentifier latestDest;
    static CommandBuffer blitCommandBuffer;



    ///<summary>Set up the blit targets and the command buffer used in the blit loop. Remember to release these targets at the end of rendering</summary>
    public static void SetupBlitTargets(CommandBuffer blitCommandBuffer, RenderTextureDescriptor blitSourceDescriptor) 
    {
        if (blitCommandBuffer == null) 
        {
            Debug.LogError("Blit Command Buffer is null, cannot set up blit targets.");
        }

        RenderTextureDescriptor descriptor = blitSourceDescriptor;
        descriptor.depthBufferBits = 0;

        blitCommandBuffer.GetTemporaryRT(blitTargetA, descriptor, FilterMode.Bilinear);
        blitCommandBuffer.GetTemporaryRT(blitTargetB, descriptor, FilterMode.Bilinear);
        BlitUtility.blitCommandBuffer = blitCommandBuffer;
    }


    ///<summary>Begins the blit loop using a source texture</summary>
    public static void BeginBlitLoop(RenderTargetIdentifier source) 
    {
        latestDest = source;
    }


    ///<summary>Performs a blit operation on two temporary swap textures</summary>
    public static void BlitNext(Material material, int pass = 0) 
    {
        var first = latestDest;
        var last = first == destinationA ? destinationB : destinationA;

        blitCommandBuffer.SetGlobalTexture(currentBlitTarget, first);
            
        blitCommandBuffer.Blit(first, last, material, pass);
        latestDest = last;
    }


    ///<summary>Ends the blit loop by blitting to the destination</summary>
    public static void EndBlitLoop(RenderTargetIdentifier destination) 
    {
        blitCommandBuffer.Blit(latestDest, destination);
    }

    
    ///<summary>Releases allocted textures</summary>
    public static void ReleaseBlitTargets() 
    {
        blitCommandBuffer.ReleaseTemporaryRT(blitTargetA);
        blitCommandBuffer.ReleaseTemporaryRT(blitTargetB);
    }
}