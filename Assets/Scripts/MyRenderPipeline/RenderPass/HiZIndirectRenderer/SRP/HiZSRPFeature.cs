using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
    public class HiZSRPFeature : ScriptableRendererFeature
    {
        private HiZSRPRenderPass hiZIndirectRenderPass;
    
        public override void Create()
        {
            hiZIndirectRenderPass = new HiZSRPRenderPass();
            hiZIndirectRenderPass.Init();
            
            hiZIndirectRenderPass.renderPassEvent =
                RenderPassEvent.BeforeRenderingPrepasses;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(hiZIndirectRenderPass);
        }
        
        private void OnDestroy()
        {
            hiZIndirectRenderPass.OnDestroy();
        }
        
    }
}
