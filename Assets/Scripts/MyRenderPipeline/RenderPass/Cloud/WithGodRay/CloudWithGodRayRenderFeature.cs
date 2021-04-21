using MyRenderPipeline.RenderPass.Cloud.ImageEffect;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.WithGodRay
{
	public class CloudWithGodRayRenderFeature : ScriptableRendererFeature
	{
		public bool enable = true;

		public Shader cloudShader;

		public Texture3D shapeTexture;
		public Texture3D detailTexture;
		public Texture2D weatherMap;
		public Texture2D blueNoise;
		public Texture2D maskNoise;

		private CloudWithGodRayRenderPass cloudWithGodRayRenderPass;

		private Material cloudMaterial;


		public override void Create()
		{
			if (cloudMaterial != null && cloudMaterial.shader != cloudShader)
			{
				DestroyImmediate(cloudMaterial);
			}
			
			if (cloudShader == null)
			{
				Debug.LogError("Shader is null!");
				return;
			}

			cloudWithGodRayRenderPass = new CloudWithGodRayRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
			};

			cloudMaterial = CoreUtils.CreateEngineMaterial(cloudShader);

			cloudWithGodRayRenderPass.Init(cloudMaterial, shapeTexture, detailTexture
				, weatherMap, blueNoise, maskNoise);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable && renderingData.postProcessingEnabled
			           && cloudWithGodRayRenderPass != null) //&& Application.isPlaying)
			{
				var cloudSettings = VolumeManager.instance.stack.GetComponent<CloudWithGodRayPostProcess>();
				if (cloudSettings != null && cloudSettings.IsActive())
				{
					cloudWithGodRayRenderPass.Setup(cloudSettings);
					renderer.EnqueuePass(cloudWithGodRayRenderPass);
				}
			}
		}
	}
}