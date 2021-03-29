using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	public class SolidCloudRenderPass : ScriptableRenderPass
	{
		private const string k_SolidCloudPass = "SolidCloud";

		private static readonly int NoiseTex_ID = Shader.PropertyToID("_NoiseTex");

		private Material solidCloudMaterial;
		private Texture2D noiseTex;

		public void Init(Material solidCloudMaterial, Texture2D noiseTex)
		{
			this.solidCloudMaterial = solidCloudMaterial;
			this.noiseTex = noiseTex;
		}

		public void Setup()
		{
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(k_SolidCloudPass);
			var sampler = new ProfilingSampler(k_SolidCloudPass);

			using (new ProfilingScope(cmd, sampler))
			{
				solidCloudMaterial.SetTexture(NoiseTex_ID, noiseTex);
				solidCloudMaterial.SetVector("_CloudDistance", new Vector4(0.119f, 0.1f, 2000, 1311));
				solidCloudMaterial.SetVector("_CloudData", new Vector4(0, 39, 1, 0.0032f));
				solidCloudMaterial.SetVector("_CloudWindDir", new Vector3(1, 0, 0));
				solidCloudMaterial.SetVector("_CloudStepping", new Vector4(0.111111f, 0.0196f, 0.00039f, 0));
				solidCloudMaterial.SetVector("_CloudColor", new Vector4(1.648f, 1.532f, 1.544f, 1.1f));
				solidCloudMaterial.SetVector("_CloudAreaPosition", new Vector4(0f, 0f, 0f, 0f));
				solidCloudMaterial.SetVector("_CloudAreaData", new Vector4(0.0081f, 0.0099f, 0.0129f, 1));

				int tid = Shader.PropertyToID("_TempRT");
				// cmd.GetTemporaryRT(tid,1920,1080,0, FilterMode.Point,GraphicsFormat.R8G8B8A8_UNorm);
				// cmd.SetRenderTarget(tid);
				cmd.ClearRenderTarget(true, true, Color.black);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
				// cmd.ReleaseTemporaryRT(tid);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
			context.Submit();
		}
	}
}