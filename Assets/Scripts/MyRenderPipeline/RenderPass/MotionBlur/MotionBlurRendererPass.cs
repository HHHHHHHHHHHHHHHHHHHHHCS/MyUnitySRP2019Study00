using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static MyRenderPipeline.RenderPass.MotionBlur.MotionBlurRendererFeature;

namespace MyRenderPipeline.RenderPass.MotionBlur
{
	public class MotionBlurRendererPass : ScriptableRenderPass
	{
		private MotionBlurSettings settings;
		private ProfilingSampler profilingSampler;

		private RenderTargetIdentifier source { get; set; }
		private RenderTargetHandle destination { get; set; }

		private Matrix4x4 prevViewProjM = Matrix4x4.identity;
		private bool isFirstVP;


		public MotionBlurRendererPass(MotionBlurSettings _settings)
		{
			settings = _settings;
			profilingSampler = new ProfilingSampler("Motion Blur");
			isFirstVP = true;
		}

		public void Setup(RenderTargetIdentifier src, RenderTargetHandle dest)
		{
			source = src;
			destination = dest;
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get("Motion Blur");

			using (new ProfilingScope(cmd, profilingSampler))
			{
				RenderMotionBlur(cmd, context, ref renderingData);
			}
		}

		private void RenderMotionBlur(CommandBuffer cmd, ScriptableRenderContext context,
			ref RenderingData renderingData)
		{
			Camera camera = renderingData.cameraData.camera;

			if (camera.cameraType == CameraType.SceneView)
			{
				return;
			}

			if (settings.intensity == 0)
			{
				return;
			}

			var mat = settings.motionBlurMaterial;

			if (mat == null)
			{
				return;
			}

			//这是必需的，因为Blit会将viewproj矩阵重置为identity
			//依赖于setupCameraProperty而不是处理它自己的矩阵。
			var proj = camera.nonJitteredProjectionMatrix;
			var view = camera.worldToCameraMatrix;
			var viewProj = proj * view;

			mat.SetMatrix("_ViewProjM", viewProj);

			mat.SetMatrix("_PreviewProjM", isFirstVP ? viewProj : prevViewProjM);

			mat.SetFloat("_Intensity", settings.intensity);
			mat.SetFloat("_Clamp", settings.clamp);

			Blit(cmd, source, destination.Identifier(), mat, (int) settings.quality);

			prevViewProjM = viewProj;
		}
	}
}