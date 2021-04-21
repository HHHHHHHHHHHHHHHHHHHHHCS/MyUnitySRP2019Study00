using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.GodRay
{
	public class GodRayRenderFeature : ScriptableRendererFeature
	{
		public bool enable = true;

		public Shader godRayShader;

		private GodRayRenderPass godRayRenderPass;
		private Material godRayMaterial;

		public override void Create()
		{
			if (godRayMaterial != null && godRayMaterial.shader != godRayShader)
			{
				DestroyImmediate(godRayMaterial);
			}
			
			if (godRayShader == null)
			{
				Debug.LogError("Shader is null!");
				return;
			}

			godRayMaterial = CoreUtils.CreateEngineMaterial(godRayShader);
			godRayRenderPass = new GodRayRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
			};
			godRayRenderPass.Init(godRayMaterial);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable && renderingData.postProcessingEnabled && godRayMaterial != null)
			{
				var godRaySettings = VolumeManager.instance.stack.GetComponent<GodRayPostProcess>();

				if (godRaySettings != null && godRaySettings.IsActive())
				{
					godRayRenderPass.Setup(godRaySettings);
					renderer.EnqueuePass(godRayRenderPass);
				}
			}
		}
	}
}