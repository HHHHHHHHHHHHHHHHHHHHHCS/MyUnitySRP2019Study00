using MyRenderPipeline.RenderPass.Cloud.ImageEffect;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Mathematics;

namespace MyRenderPipeline.RenderPass.GodRay
{
	public class GodRayRenderPass : ScriptableRenderPass
	{
		private const string k_GodRayPass = "GodRayPass";

		private const string c_EnableCloud = "_EnableCloud";

		private static readonly int GodRayRT_ID = Shader.PropertyToID("_GodRayRT");
		private static readonly int GodRayBlurRT_ID = Shader.PropertyToID("_GodRayBlurRT");


		private static readonly int SunUV_ID = Shader.PropertyToID("_SunUV");
		private static readonly int GodRayStrength_ID = Shader.PropertyToID("_GodRayStrength");
		private static readonly int GodRayColor_ID = Shader.PropertyToID("_GodRayColor");


		private static readonly RenderTargetIdentifier godRay_RTI = new RenderTargetIdentifier(GodRayRT_ID);
		private static readonly RenderTargetIdentifier godRayBlur_RTI = new RenderTargetIdentifier(GodRayBlurRT_ID);


		private Material godRayMaterial;

		private GodRayPostProcess godRaySettings;


		public void Init(Material mat)
		{
			godRayMaterial = mat;
		}

		public void Setup(GodRayPostProcess godRaySettings)
		{
			this.godRaySettings = godRaySettings;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			cmd.GetTemporaryRT(GodRayRT_ID, cameraTextureDescriptor);
			cmd.GetTemporaryRT(GodRayBlurRT_ID, cameraTextureDescriptor);
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			cmd.ReleaseTemporaryRT(GodRayRT_ID);
			cmd.ReleaseTemporaryRT(GodRayBlurRT_ID);
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			int mainLightIndex = renderingData.lightData.mainLightIndex;
			if (mainLightIndex < 0)
			{
				return;
			}

			var light = renderingData.lightData.visibleLights[mainLightIndex];

			var mainCamera = Camera.main;

			var w2vMatrix = new float3x3(mainCamera.worldToCameraMatrix);
			//x左右-1~1  y高低1~-1  面对z是-1  背对z是1
			var sun2uv = math.mul(w2vMatrix, -light.light.transform.forward);
			sun2uv = math.mul(mainCamera.projectionMatrix, new float4(sun2uv.xyz, 1.0f)).xyz;

			if (sun2uv.z < 0)
			{
				return;
			}

			var cmd = CommandBufferPool.Get(k_GodRayPass);
			var sampler = new ProfilingSampler(k_GodRayPass);

			using (new ProfilingScope(cmd, sampler))
			{
				var cloudSettings = VolumeManager.instance.stack.GetComponent<CloudImageEffectPostProcess>();
				bool enableCloud = cloudSettings != null && cloudSettings.IsActive();

				//1.setup------------------
				CoreUtils.SetKeyword(godRayMaterial, c_EnableCloud, enableCloud);
				godRayMaterial.SetVector(SunUV_ID, (Vector3) sun2uv);
				godRayMaterial.SetVector(GodRayStrength_ID,
					new Vector4(godRaySettings.godRayDir.value.x, godRaySettings.godRayDir.value.y
						, godRaySettings.godRayStrength.value, godRaySettings.godRayMaxDistance.value));
				godRayMaterial.SetVector(GodRayColor_ID, godRaySettings.godRayColor.value);


				cmd.SetRenderTarget(godRay_RTI);

				CoreUtils.DrawFullScreen(cmd, godRayMaterial, null, 0);


				//2.blur------------------

				cmd.SetRenderTarget(godRayBlur_RTI);

				// cmd.SetGlobalTexture(GodRayRT_ID, godRay_RTI);

				CoreUtils.DrawFullScreen(cmd, godRayMaterial, null, 1);

				//3.composite------------------
				
				// cmd.SetRenderTarget();

				RenderTargetIdentifier cameraTarget = new RenderTargetIdentifier("_CameraColorTexture");
				cmd.SetRenderTarget(cameraTarget);


				CoreUtils.DrawFullScreen(cmd, godRayMaterial, null, 2);


				context.ExecuteCommandBuffer(cmd);
				context.Submit();
				CommandBufferPool.Release(cmd);
			}
		}
	}
}