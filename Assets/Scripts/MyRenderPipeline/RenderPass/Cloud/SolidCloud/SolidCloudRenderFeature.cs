using MyRenderPipeline.RenderPass.GodRay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	public class SolidCloudRenderFeature : ScriptableRendererFeature
	{
		public bool enable = true;
		public Shader solidCloudShader;
		public Texture2D noiseTex;

		private Material solidCloudMaterial;
		private SolidCloudRenderPass solidCloudRenderPass;

		public override void Create()
		{
			if (solidCloudShader == null)
			{
				Debug.LogError("Shader is null!");
				return;
			}

			solidCloudMaterial = CoreUtils.CreateEngineMaterial(solidCloudShader);
			solidCloudRenderPass = new SolidCloudRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
			};
			solidCloudRenderPass.Init(solidCloudMaterial, noiseTex);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable && renderingData.postProcessingEnabled && solidCloudMaterial != null)
			{
				var settings = VolumeManager.instance.stack.GetComponent<SolidCloudRenderPostProcess>();

				if (settings != null && settings.IsActive())
				{
					solidCloudRenderPass.Setup(settings);
					renderer.EnqueuePass(solidCloudRenderPass);
				}
			}
		}
	}
}