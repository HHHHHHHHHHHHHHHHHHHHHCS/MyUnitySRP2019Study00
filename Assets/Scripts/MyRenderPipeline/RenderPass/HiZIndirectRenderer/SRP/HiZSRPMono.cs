using System;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
    public class HiZSRPMono : MonoBehaviour
    {
        private void Update()
        {
            if (HiZSRPRenderPass.graphicsRenderQueue != null)
            {
                HiZSRPRenderPass.graphicsRenderQueue();
                HiZSRPRenderPass.graphicsRenderQueue = null;
            }
        }

        private void OnDrawGizmos()
        {
            if (HiZSRPRenderPass.gizmosQueue != null)
            {
                HiZSRPRenderPass.gizmosQueue();
            }
        }
    }
}
