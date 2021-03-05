using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.EasyHeightFog
{
	public class EasyHeightFogFeatures : ScriptableRendererFeature
	{
		public bool enable = true;

		public Shader easyHeightFogShader;
		
		private EasyHeightFogRenderPass easyHeightFogRenderPass;
		private Material easyHeightFogMaterial;
		
		public override void Create()
		{
			if (easyHeightFogMaterial != null)
			{
				Destroy(easyHeightFogMaterial);
			}

			if (easyHeightFogShader != null)
			{
				easyHeightFogMaterial = new Material(easyHeightFogShader);
				
				easyHeightFogRenderPass = new EasyHeightFogRenderPass(easyHeightFogMaterial)
				{
					renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
				};
			}

		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable && easyHeightFogRenderPass!= null)
			{
				renderer.EnqueuePass(easyHeightFogRenderPass);
			}
		}
	}
}