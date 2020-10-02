using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.GrassCulling
{
    public class GrassCullingRendererFeature : ScriptableRendererFeature
    {
        private GrassCullingRenderPass grassCullingRenderPass;
        
        public override void Create()
        {
            grassCullingRenderPass = new GrassCullingRenderPass();
            grassCullingRenderPass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(grassCullingRenderPass);
        }
    }
}
