using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	public class TAAURPRenderPass : ScriptableRenderPass
	{
		private const string k_TAAURPPass = "TAA_URP";
		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_TAAURPPass);

		private readonly RenderTargetIdentifier CameraColorTexture_RTI =
			new RenderTargetIdentifier("_CameraColorTexture");

		internal static readonly string k_TAA_LOW_QUALITY = "_TAA_LOW";
		internal static readonly string k_TAA_MEDIUM_QUALITY = "_TAA_MEDIUM";
		internal static readonly string k_TAA_HIGH_QUALITY = "_TAA_HIGH";

		public static readonly int SrcTex_ID = Shader.PropertyToID("_SrcTex");
		public static readonly int TAA_Params_ID = Shader.PropertyToID("_TAA_Params");
		public static readonly int TAA_PreTexture_ID = Shader.PropertyToID("_TAA_PreTexture");
		public static readonly int TAA_PrevViewProj_ID = Shader.PropertyToID("_TAA_PrevViewProj");
		public static readonly int TAA_Current_I_V_Jittered_ID = Shader.PropertyToID("_TAA_Current_I_V_Jittered");
		public static readonly int TAA_Current_I_P_Jittered_ID = Shader.PropertyToID("_TAA_Current_I_P_Jittered");

		private Material taaurpMat;
		private TAAURPPostProcess settings;
		private TAAURPData data;

		private RenderTexture[] historyRTs;
		private int indexWrite = 0;


		public void Init(Material taaurpMaterial)
		{
			taaurpMat = taaurpMaterial;
		}

		public void Setup(TAAURPPostProcess taaSettings, ref TAAURPData taaData)
		{
			settings = taaSettings;
			data = taaData;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			int width = cameraTextureDescriptor.width;
			int height = cameraTextureDescriptor.height;
			RenderTextureFormat format = cameraTextureDescriptor.colorFormat;
			int depth = 0; //用不到depth
			FilterMode filterMode = FilterMode.Point; // blit 没有缩放 用不到滤波
			int msaa = 1; // blit 用不到msaa

			if (CheckRT(2, width, height, format, filterMode, depth, msaa))
			{
				ClearRT();
				CreateRT(2, width, height, format, filterMode, depth, msaa);
			}
		}

		public void CreateRT(int count, int width, int height, RenderTextureFormat format, FilterMode filterMode
			, int depthBits = 0, int antiAliasing = 1)
		{
			historyRTs = new RenderTexture[count];
			for (int i = 0; i < count; i++)
			{
				historyRTs[i] = RenderTexture.GetTemporary(width, height, depthBits, format,
					RenderTextureReadWrite.Default, antiAliasing);
			}
		}

		public bool CheckRT(int count, int width, int height, RenderTextureFormat format, FilterMode filterMode
			, int depthBits = 0, int antiAliasing = 1)
		{
			if (historyRTs == null)
			{
				return true;
			}

			if (historyRTs.Length != count)
			{
				return true;
			}

			// return historyRTs.Any(rt => rt == null || rt.width != width || rt.height != height || rt.format != format || rt.filterMode != filterMode || rt.depth != depthBits || rt.antiAliasing != antiAliasing);

			foreach (var rt in historyRTs)
			{
				if (rt == null || rt.width != width || rt.height != height
				    || rt.format != format || rt.filterMode != filterMode
				    || rt.depth != depthBits || rt.antiAliasing != antiAliasing)
				{
					return true;
				}
			}

			return false;
		}

		public void ClearRT()
		{
			if (historyRTs != null)
			{
				foreach (var rt in historyRTs)
				{
					if (rt != null)
					{
						RenderTexture.ReleaseTemporary(rt);
					}
				}

				historyRTs = null;
			}
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get(k_TAAURPPass);
			using (new ProfilingScope(cmd, profilingSampler))
			{
				DoTemporalAntiAliasing(cmd, ref renderingData.cameraData);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		private void DoTemporalAntiAliasing(CommandBuffer cmd, ref CameraData cameraData)
		{
			int indexRead = indexWrite;
			indexWrite = (indexWrite + 1) % 2;

			Matrix4x4 inv_Proj_Jittered = Matrix4x4.Inverse(data.proOverride);
			Matrix4x4 inv_View_Jittered = Matrix4x4.Inverse(data.viewCurrent);
			Matrix4x4 previous_vp = data.projPrevious * data.viewPrevious;

			taaurpMat.SetMatrix(TAA_Current_I_V_Jittered_ID, inv_View_Jittered);
			taaurpMat.SetMatrix(TAA_Current_I_P_Jittered_ID, inv_Proj_Jittered);
			taaurpMat.SetMatrix(TAA_PrevViewProj_ID, previous_vp);
			taaurpMat.SetVector(TAA_Params_ID,
				new Vector4(data.sampleOffset.x, data.sampleOffset.y, settings.feedback.value, 0));
			taaurpMat.SetTexture(TAA_PreTexture_ID, historyRTs[indexRead]);
			CoreUtils.SetKeyword(cmd, k_TAA_LOW_QUALITY,
				settings.quality.value == TAAURPQuality.Low);
			CoreUtils.SetKeyword(cmd, k_TAA_MEDIUM_QUALITY,
				settings.quality.value == TAAURPQuality.Medium);
			CoreUtils.SetKeyword(cmd, k_TAA_HIGH_QUALITY,
				settings.quality.value == TAAURPQuality.High);


			cmd.SetRenderTarget(historyRTs[indexWrite]
				, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			cmd.SetGlobalTexture(SrcTex_ID, CameraColorTexture_RTI);
			CoreUtils.DrawFullScreen(cmd, taaurpMat, null, 0);


			cmd.SetRenderTarget(CameraColorTexture_RTI, RenderBufferLoadAction.DontCare,
				RenderBufferStoreAction.Store);
			cmd.SetGlobalTexture(SrcTex_ID, historyRTs[indexWrite]);
			CoreUtils.DrawFullScreen(cmd, taaurpMat, null, 1);
		}
	}
}