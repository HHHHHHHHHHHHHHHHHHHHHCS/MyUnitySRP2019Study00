using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static MyRenderPipeline.RenderPass.MotionBlur.MotionBlurRendererFeature;

namespace MyRenderPipeline.RenderPass.MotionBlur
{
	public class MotionBlurRendererPass : ScriptableRenderPass
	{
		private const string k_profilingTag = "Motion Blur";
		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_profilingTag);

		private MotionBlurSettings settings;

		private RenderTargetIdentifier source { get; set; }
		private RenderTargetIdentifier destination { get; set; }

		private Matrix4x4 prevViewProjM = Matrix4x4.identity;
		private bool isFirstVP;


		public MotionBlurRendererPass(MotionBlurSettings _settings)
		{
			settings = _settings;
			isFirstVP = true;
		}

		public void Setup(RenderTargetIdentifier src, RenderTargetIdentifier dest)
		{
			source = src;
			destination = dest;
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get(k_profilingTag);

			using (new ProfilingScope(cmd, profilingSampler))
			{
				RenderMotionBlur(cmd, context, ref renderingData);
			}
			
			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		private void RenderMotionBlur(CommandBuffer cmd, ScriptableRenderContext context,
			ref RenderingData renderingData)
		{
			Camera camera = renderingData.cameraData.camera;
			
			if (renderingData.cameraData.camera.cameraType != CameraType.Game)
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

			mat.SetMatrix("_PrevViewProjM", isFirstVP ? viewProj : prevViewProjM);

			mat.SetFloat("_Intensity", settings.intensity);
			mat.SetFloat("_Clamp", settings.clamp);

			// cmd.SetGlobalTexture("_MainTex", source);
			// cmd.SetRenderTarget(destination);
			// cmd.ClearRenderTarget(true, true, Color.black);
			// cmd.DrawMesh(RenderUtils.FullScreenMesh, Matrix4x4.identity, mat, 0, (int) settings.quality);
			cmd.BlitFullScreen(source, destination, mat, (int) settings.quality);


			prevViewProjM = viewProj;
			isFirstVP = false;


		}
	}
}