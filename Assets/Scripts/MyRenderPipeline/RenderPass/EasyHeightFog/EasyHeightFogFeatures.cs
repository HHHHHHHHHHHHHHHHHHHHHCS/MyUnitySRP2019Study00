using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.EasyHeightFog
{
	public class EasyHeightFogFeatures : ScriptableRendererFeature
	{
		public bool enable = true;

		private EasyHeightFogRenderPass easyHeightFogRenderPass;

		public override void Create()
		{
			easyHeightFogRenderPass = new EasyHeightFogRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
			};
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable)
			{
				renderer.EnqueuePass(easyHeightFogRenderPass);
			}
		}
	}
}