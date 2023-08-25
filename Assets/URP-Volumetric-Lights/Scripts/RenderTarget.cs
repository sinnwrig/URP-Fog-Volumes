using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;


public struct RenderTarget
{
    public readonly string name;
    public readonly int id;
    public readonly RenderTargetIdentifier identifier;


    public bool isAssigned { get; private set; }
    public RenderTextureDescriptor descriptor { get; private set; }



    public RenderTarget(string propertyName) 
    {
        name = propertyName;
        id = Shader.PropertyToID(propertyName);
        identifier = new RenderTargetIdentifier(id);
        
        descriptor = default;
        isAssigned = false;
    }


    public void GetTemporary(CommandBuffer cmd, RenderTextureDescriptor desc, FilterMode filter)
    {
        cmd.GetTemporaryRT(id, desc, filter);
        
        descriptor = desc;
        isAssigned = true;
    }


    public void GetTemporary(CommandBuffer cmd, int width, int height, int depthBuffer = 0, RenderTextureFormat format = RenderTextureFormat.ARGBFloat, FilterMode filter = FilterMode.Bilinear)
    {
        cmd.GetTemporaryRT(id, width, height, depthBuffer, filter, format);
        
        descriptor = new RenderTextureDescriptor() { width = width, height = height, depthBufferBits = depthBuffer, colorFormat = format };
        isAssigned = true;
    }


    public void ReleaseTemporary(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(id);
        
        descriptor = default;
        isAssigned = false;
    }
}
