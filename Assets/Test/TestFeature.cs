using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TestFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Thingy
    {
        public Material mat;
        public Mesh mesh;
        public Vector3 pos;
        public Vector3 rot;
        public Vector3 scl;

        public Matrix4x4 mtr => Matrix4x4.TRS(pos, Quaternion.Euler(rot), scl);
    }


    public Thingy[] objects = new Thingy[1];



    class CustomRenderPass : ScriptableRenderPass
    {
        public TestFeature feature;

        private static readonly int tempId = Shader.PropertyToID("_TempRenderTarget");
        private static readonly RenderTargetIdentifier rtId = new(tempId);


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cmd.GetTemporaryRT(tempId, renderingData.cameraData.cameraTargetDescriptor, FilterMode.Point);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("Test Render Feature");

            cmd.SetRenderTarget(rtId);
            cmd.ClearRenderTarget(true, true, Color.black);

    
            for (int i = 0; i < feature.objects.Length; i++)
            {
                Thingy t = feature.objects[i];

                if (t.mat != null && t.mesh != null)
                {
                    cmd.DrawMesh(t.mesh, t.mtr, t.mat, 0, 0);
                }
            }

            cmd.Blit(rtId, renderingData.cameraData.renderer.cameraColorTargetHandle);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(tempId);
        }
    }


    CustomRenderPass m_ScriptablePass;
    public Mesh cone;


    public override void Create()
    {
        
        cone = MeshUtility.ConeMesh;

        m_ScriptablePass = new CustomRenderPass
        {
            feature = this,
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents
        };
    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!renderingData.cameraData.isPreviewCamera)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}


