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
    }
}
