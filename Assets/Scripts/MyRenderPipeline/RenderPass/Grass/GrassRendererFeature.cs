using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Grass
{
	public class GrassRendererFeature : ScriptableRendererFeature
	{
		private GrassRenderPass grassRenderPass;

		public override void Create()
		{
			//Shader.globalRenderPipeline = "UniversalRenderPipeline";
		
			grassRenderPass = new GrassRenderPass();

			// 注入渲染的哪个阶段
			grassRenderPass.renderPassEvent =
				RenderPassEvent.AfterRenderingPrePasses; //不想切换RT _CameraColorTexture, 所以使用 AfterRenderingPrePasses
		}

		//在这里，可以在渲染器中注入一个或多个渲染过程。
		//在为每个摄影机设置一次渲染器时调用此方法。
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			renderer.EnqueuePass(grassRenderPass);
		}
	}
}