using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class CloudImageEffectRenderFeature : ScriptableRendererFeature
	{
		public bool enable = true;

		public Shader shader;
		public Texture2D blueNoise;

		private CloudImageEffectRenderPass cloudImageEffectRenderPass;

		private Material material;

		public override void Create()
		{
			if (shader == null)
			{
				Debug.LogError("Shader is null!");
				return;
			}

			material = CoreUtils.CreateEngineMaterial(shader);

			cloudImageEffectRenderPass = new CloudImageEffectRenderPass()
			{
				renderPassEvent = RenderPassEvent.AfterRenderingOpaques
			};

			cloudImageEffectRenderPass.Init(material, blueNoise);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable && cloudImageEffectRenderPass != null)
			{
				cloudImageEffectRenderPass.Setup();
				renderer.EnqueuePass(cloudImageEffectRenderPass);
			}
		}
	}
}