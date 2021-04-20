using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	public class TAAURPRenderPass : ScriptableRenderPass
	{
		private const string k_TAAURPPass = "TAA_URP";
		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_TAAURPPass);


		private Material taaurpMat;
		private TAAURPPostProcess settings;
		private TAAURPData data;

		public void Init(Material taaurpMaterial)
		{
			taaurpMat = taaurpMaterial;
		}

		public void Setup(TAAURPPostProcess taaSettings, ref TAAURPData taaData)
		{
			settings = taaSettings;
			data = taaData;
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get(k_TAAURPPass);
			using (new ProfilingScope(cmd, profilingSampler))
			{
				cmd.SetProjectionMatrix(data.proOverride);
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				DoTemporalAntiAliasing(cmd, ref renderingData.cameraData);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				cmd.SetProjectionMatrix(data.projCurrent);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		private void DoTemporalAntiAliasing(CommandBuffer cmd, ref CameraData cameraData)
		{
		}
	}
}