using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Common
{
	[CreateAssetMenu(fileName = "TransparentPass", menuName = "MyRP/RenderPass/Transparent")]
	public class TransparentPass : MyRenderPassAsset
	{
		public bool useDepthPeeling = false;

		[Range(1, 16)] public int depthPeelingPass = 4;

		public override MyRenderPass CreateRenderPass()
		{
			return new TransparentPassRenderer(this);
		}
	}

	public class TransparentPassRenderer : MyRenderPassRenderer<TransparentPass>
	{
		private const string k_profilerTag_transparent = "RenderTransparent";
		private readonly ProfilingSampler profilingSampler_transparent = new ProfilingSampler(k_profilerTag_transparent);
		private const string k_profilerTag_depthPeeling = "Depth Peeling";
		private readonly ProfilingSampler profilingSampler_depthPeeling = new ProfilingSampler(k_profilerTag_depthPeeling);
		
		private Material copyDepthMat;
		private Material transparentMat;

		public TransparentPassRenderer(TransparentPass asset) : base(asset)
		{
			//可能有泄漏  没有清理
			copyDepthMat = new Material(Shader.Find("MyRP/CopyDepth"));
			transparentMat = new Material(Shader.Find("MyRP/Transparent"));
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(k_profilerTag_transparent);

			using (new ProfilingScope(cmd, profilingSampler_transparent))
			{
				cmd.SetRenderTarget(renderingData.colorTarget, renderingData.depthTarget);

				if (!asset.useDepthPeeling)
				{
					RenderDefaultTransparent(context, ref renderingData);
				}
				else
				{
					RenderDepthPeeling(context, ref renderingData);
				}
			}
			
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);

		}

		private void RenderDefaultTransparent(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var camera = renderingData.camera;
			FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
			SortingSettings sortingSettings = new SortingSettings(camera);
			sortingSettings.criteria = SortingCriteria.CommonTransparent;
			DrawingSettings drawingSettings = new DrawingSettings(new ShaderTagId("TransparentBack"), sortingSettings)
			{
				enableDynamicBatching = false,
				perObjectData = PerObjectData.ReflectionProbes,
			};
			drawingSettings.SetShaderPassName(1, new ShaderTagId("TransparentFront"));
			drawingSettings.SetShaderPassName(1, new ShaderTagId("Transparent"));

			RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Nothing);

			context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
				ref stateBlock);
		}

		private void RenderDepthPeeling(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var camera = renderingData.camera;

			FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
			SortingSettings sortingSettings = new SortingSettings(camera);
			sortingSettings.criteria = SortingCriteria.CommonTransparent;
			DrawingSettings drawingSettings =
				new DrawingSettings(new ShaderTagId("DepthPeelingFirstPass"), sortingSettings)
				{
					enableDynamicBatching = false,
					perObjectData = PerObjectData.ReflectionProbes,
				};

			RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Nothing);

			var cmd = CommandBufferPool.Get(k_profilerTag_depthPeeling);
			using (new ProfilingScope(cmd, profilingSampler_depthPeeling))
			{
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				List<int> colorRTs = new List<int>(asset.depthPeelingPass);
				List<int> depthRTs = new List<int>(asset.depthPeelingPass);

				for (var i = 0; i < asset.depthPeelingPass; i++)
				{
					colorRTs.Add(Shader.PropertyToID($"_DepthPeelingColor{i}"));
					depthRTs.Add(Shader.PropertyToID($"_DepthPeelingDepth{i}"));
					cmd.GetTemporaryRT(colorRTs[i], camera.pixelWidth, camera.pixelHeight, 0);
					cmd.GetTemporaryRT(depthRTs[i], camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point,
						RenderTextureFormat.RFloat);

					if (i == 0)
					{
						drawingSettings.SetShaderPassName(0, new ShaderTagId("DepthPeelingFirstPass"));


						cmd.SetRenderTarget(new RenderTargetIdentifier[] {colorRTs[i], depthRTs[i]}, depthRTs[i]);
						cmd.ClearRenderTarget(true, true, Color.black);

						// cmd.Blit(renderingData.depthTarget, depthRTs[i], copyDepthMat, 0);
						// cmd.SetRenderTarget(new RenderTargetIdentifier[] {colorRTs[i], depthRTs[i]}, depthRTs[i]);


						context.ExecuteCommandBuffer(cmd);
						cmd.Clear();

						context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
							ref stateBlock);
					}
					else
					{
						cmd.SetGlobalTexture("_MaxDepthTex", depthRTs[i - 1]);
						drawingSettings.SetShaderPassName(0, new ShaderTagId("DepthPeelingPass"));

						cmd.SetRenderTarget(new RenderTargetIdentifier[] {colorRTs[i], depthRTs[i]}, depthRTs[i]);
						cmd.ClearRenderTarget(true, true, Color.black);
						context.ExecuteCommandBuffer(cmd);
						cmd.Clear();

						context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
							ref stateBlock);
					}
				}

				cmd.SetRenderTarget(renderingData.colorTarget, renderingData.depthTarget);
				for (var i = asset.depthPeelingPass - 1; i >= 0; i--)
				{
					cmd.SetGlobalTexture("_DepthTex", depthRTs[i]);
					cmd.Blit(colorRTs[i], renderingData.colorTarget, transparentMat, i == 0 ? 5 : 4);

					cmd.ReleaseTemporaryRT(colorRTs[i]);
					cmd.ReleaseTemporaryRT(depthRTs[i]);
				}

				cmd.SetRenderTarget(renderingData.colorTarget, renderingData.depthTarget);

			}
			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}