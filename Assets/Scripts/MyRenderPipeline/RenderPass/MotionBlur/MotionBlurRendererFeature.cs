using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.MotionBlur
{
	public class MotionBlurRendererFeature : ScriptableRendererFeature
	{
		[System.Serializable]
		public enum MotionBlurQuality
		{
			Low,
			Medium,
			High
		}

		[System.Serializable]
		public class MotionBlurSettings
		{
			public MotionBlurQuality quality = MotionBlurQuality.Low;
			[Range(0f, 1f)] public float intensity = 1f;
			[Range(0f, 0.2f)] public float clamp = 0.05f;
			public Material motionBlurMaterial;
		}

		public MotionBlurSettings settings;

		private MotionBlurRendererPass motionBlurRendererPass;

		private RenderTargetIdentifier src_RTI;

		public override void Create()
		{
			src_RTI = new RenderTargetIdentifier("_CameraOpaqueTexture");
			
			motionBlurRendererPass = new MotionBlurRendererPass(settings);

			motionBlurRendererPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			var src = src_RTI;
			var dest = renderer.cameraColorTarget;

			motionBlurRendererPass.Setup(src, dest);

			renderer.EnqueuePass(motionBlurRendererPass);
		}
	}
}