using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.TAAURP
{
    public class TAAURPProjectionRenderPass : ScriptableRenderPass
    {
        private const string k_TAAURPPass = "TAA_URP";
        private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_TAAURPPass);

        private Matrix4x4 projectionMatrix;
        
        public void Setup(Matrix4x4 proj)
        {
            projectionMatrix = proj;
        }
        
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(k_TAAURPPass);
            using (new ProfilingScope(cmd, profilingSampler))
            {
                cmd.SetProjectionMatrix(projectionMatrix);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
