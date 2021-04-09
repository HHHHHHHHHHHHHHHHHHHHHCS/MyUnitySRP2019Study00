using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.CrepuscularRay
{
	public class CrepuscularRayRenderPass : ScriptableRenderPass
	{
		private const string k_CrepuscularRayRenderPass = "CrepuscularRayRenderPass";


		private static readonly int RayRange_ID = Shader.PropertyToID("_RayRange");
		private static readonly int RayIntensity_ID = Shader.PropertyToID("_RayIntensity");
		private static readonly int RayPower_ID = Shader.PropertyToID("_RayPower");
		private static readonly int LightThreshold_ID = Shader.PropertyToID("_LightThreshold");
		private static readonly int QualityStep_ID = Shader.PropertyToID("_QualityStep");
		private static readonly int OffsetUV_ID = Shader.PropertyToID("_OffsetUV");
		private static readonly int BoxBlur_ID = Shader.PropertyToID("_BoxBlur");
		private static readonly int LightColor_ID = Shader.PropertyToID("_LightColor");
		private static readonly int LightViewPos_ID = Shader.PropertyToID("_LightViewPos");
		private static readonly int DownsampleTex_ID = Shader.PropertyToID("_DownsampleTex");


		private static readonly RenderTargetIdentifier DownsampleTex_RTI =
			new RenderTargetIdentifier(DownsampleTex_ID);

		private static readonly RenderTargetIdentifier CameraColorTex_RTI =
			new RenderTargetIdentifier("_CameraColorTexture");

		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_CrepuscularRayRenderPass);

		private Material rayMaterial;
		private CrepuscularRayPostProcess raySettings;
		private RenderTextureDescriptor desc;

		public void Init(Material rayMaterial)
		{
			this.rayMaterial = rayMaterial;
		}

		public void Setup(CrepuscularRayPostProcess raySettings)
		{
			this.raySettings = raySettings;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			desc = cameraTextureDescriptor;
			desc.depthBufferBits = 0;
			desc.colorFormat = RenderTextureFormat.DefaultHDR;
			desc.width = desc.width / raySettings.downsample.value;
			desc.height = desc.height / raySettings.downsample.value;
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			int mainLightIndex = renderingData.lightData.mainLightIndex;
			if (mainLightIndex < 0)
			{
				return;
			}

			var mainLight = renderingData.lightData.visibleLights[mainLightIndex].light;
			var forward = -mainLight.transform.forward;
			var camera = renderingData.cameraData.camera;
			Vector3 lightPos = camera.WorldToViewportPoint(camera.transform.position +
			                                               forward * camera.farClipPlane);
			if (lightPos.z < 0)
			{
				return;
			}

			var cmd = CommandBufferPool.Get(k_CrepuscularRayRenderPass);

			using (new ProfilingScope(cmd, profilingSampler))
			{
				rayMaterial.SetFloat(RayRange_ID, raySettings.rayRange.value);
				rayMaterial.SetFloat(RayIntensity_ID, raySettings.rayIntensity.value);
				rayMaterial.SetFloat(RayPower_ID, raySettings.rayPower.value);
				rayMaterial.SetFloat(LightThreshold_ID, raySettings.lightThreshold.value);
				rayMaterial.SetInt(QualityStep_ID, raySettings.qualityStep.value);
				rayMaterial.SetFloat(OffsetUV_ID, raySettings.offsetUV.value);
				rayMaterial.SetFloat(BoxBlur_ID, raySettings.boxBlur.value);
				rayMaterial.SetColor(LightColor_ID, raySettings.lightColor.value);
				rayMaterial.SetVector(LightViewPos_ID, new Vector4(lightPos.x, lightPos.y, 0, 0));

				//downsample========================
				cmd.GetTemporaryRT(DownsampleTex_ID, desc);
				cmd.SetRenderTarget(DownsampleTex_RTI, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, rayMaterial, null, 1);
				cmd.SetGlobalTexture(DownsampleTex_ID, DownsampleTex_RTI);

				//crepuscularRay========================
				cmd.SetRenderTarget(CameraColorTex_RTI, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, rayMaterial, null, 0);

				cmd.ReleaseTemporaryRT(DownsampleTex_ID);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}