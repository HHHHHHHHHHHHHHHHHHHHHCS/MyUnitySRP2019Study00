using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class CloudImageEffectRenderFeature : ScriptableRendererFeature
	{
		public bool enable = true;

		public Shader cloudShader;
		public Shader cloudSkyShader;

		public Texture3D shapeTexture;
		public Texture3D detailTexture;
		public Texture2D weatherMap;
		public Texture2D blueNoise;

		private CloudImageEffectRenderPass cloudImageEffectRenderPass;

		private Material cloudMaterial;
		private Material cloudSkyMaterial;


		public override void Create()
		{
			if (cloudShader == null || cloudSkyShader == null)
			{
				Debug.LogError("Shader is null!");
				return;
			}

			cloudMaterial = CoreUtils.CreateEngineMaterial(cloudShader);
			cloudSkyMaterial = CoreUtils.CreateEngineMaterial(cloudSkyShader);

			cloudImageEffectRenderPass = new CloudImageEffectRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingTransparents
			};

			cloudImageEffectRenderPass.Init(cloudMaterial, cloudSkyMaterial
				, shapeTexture, detailTexture, weatherMap, blueNoise);
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (enable && cloudImageEffectRenderPass != null) //&& Application.isPlaying)
			{
				cloudImageEffectRenderPass.Setup();
				renderer.EnqueuePass(cloudImageEffectRenderPass);
			}
		}
	}
}