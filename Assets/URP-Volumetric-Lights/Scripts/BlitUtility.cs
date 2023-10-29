using UnityEngine;
using UnityEngine.Rendering;


public static class BlitUtility 
{   
    static readonly int blitTargetA = Shader.PropertyToID("_BlitA");
    static readonly int blitTargetB = Shader.PropertyToID("_BlitB");


    static RenderTargetIdentifier destinationA = new(blitTargetA);
    static RenderTargetIdentifier destinationB = new(blitTargetB);


    static RenderTargetIdentifier latestDest;
    public static CommandBuffer blitCommandBuffer;


    /// <summary>
    /// Call SetupBlitTargets before calling BeginBlitLoop/BlitNext.
    /// </summary>
    /// <param name="cmd">The command buffer used to blit.</param>
    /// <param name="blitSourceDescriptor">The source texture information to use.</param>
    public static void SetupBlitTargets(CommandBuffer cmd, RenderTextureDescriptor blitSourceDescriptor) 
    {
        ReleaseBlitTargets(cmd);

        if (cmd == null) 
        {
            Debug.LogError("Blit Command Buffer is null, cannot set up blit targets.");
        }

        RenderTextureDescriptor descriptor = blitSourceDescriptor;
        descriptor.depthBufferBits = 0;

        cmd.GetTemporaryRT(blitTargetA, descriptor, FilterMode.Bilinear);
        cmd.GetTemporaryRT(blitTargetB, descriptor, FilterMode.Bilinear);
    }


    /// <summary>
    /// Assigns the initial texture used in the blit loop.
    /// </summary>
    /// <param name="source">The source texture to use.</param>
    public static void BeginBlitLoop(CommandBuffer cmd, RenderTargetIdentifier source) 
    {
        blitCommandBuffer = cmd;
        latestDest = source;
    }


    /// <summary>
    /// Blits back and forth between two temporary textures until EndBlitLoop is called.
    /// </summary>
    /// <param name="material">The material to blit with.</param>
    /// <param name="shaderProperty">The shader property to assign the source texture to.</param>
    /// <param name="pass">The material pass to use.</param>
    public static void BlitNext(Material material, string shaderProperty, int pass = 0) 
    {
        if (blitCommandBuffer == null)
        {
            throw new System.Exception("No CommandBuffer has been passed in before beginning the blit loop! Make sure BeginBlitLoop() is called before calling BlitNext(), and make sure CommandBuffer is not disposed of prematurely!"); 
        }

        var first = latestDest;
        var last = first == destinationA ? destinationB : destinationA;

        blitCommandBuffer.SetGlobalTexture(shaderProperty, first);
            
        blitCommandBuffer.Blit(first, last, material, pass);
        latestDest = last;
    }


    /// <summary>
    /// Writes the final blit loop result into the destination texture.
    /// </summary>
    /// <param name="destination">The texture to write the blit loop output into.</param>
    public static void EndBlitLoop(RenderTargetIdentifier destination) 
    {
        blitCommandBuffer.Blit(latestDest, destination);
        blitCommandBuffer = null;
    }


    /// <summary>
    /// Call ReleaseBlitTargets after finishing any blit loops performed during rendering.
    /// </summary>
    /// <param name="cmd">The command buffer used to release allocated textures.</param>
    public static void ReleaseBlitTargets(CommandBuffer cmd) 
    {
        cmd.ReleaseTemporaryRT(blitTargetA);
        cmd.ReleaseTemporaryRT(blitTargetB);
    }
}