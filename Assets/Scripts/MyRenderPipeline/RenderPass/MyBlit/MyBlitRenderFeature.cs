using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.MyBlit
{
	public class MyBlitRenderFeature : ScriptableRendererFeature
	{
		public enum Downsampling
		{
			None,
			_2xBilinear,
			_4xBox,
			_4xBilinear,
		}

		public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingSkybox;

		public Downsampling downsampling;

		//unity 是用[Reload("Shaders/Utils/Sampling.shader")] 标签的
		public Shader blitShader;

		private MyBlitRenderPass blitRenderPass;

		private Material blitMaterial;

		public override void Create()
		{
			if (blitMaterial != null && blitMaterial.shader != blitShader)
			{
				DestroyImmediate(blitMaterial);
			}

			if (blitShader != null)
			{
				blitMaterial = CoreUtils.CreateEngineMaterial(blitShader);
				blitRenderPass = new MyBlitRenderPass(blitMaterial, downsampling)
				{
					renderPassEvent = this.renderPassEvent
				};
			}
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (blitRenderPass != null)
			{
				blitRenderPass.renderPassEvent = renderPassEvent;
				renderer.EnqueuePass(blitRenderPass);
			}
		}
	}
}