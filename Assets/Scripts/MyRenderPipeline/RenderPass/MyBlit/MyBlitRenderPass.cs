using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Downsampling = MyRenderPipeline.RenderPass.MyBlit.MyBlitRenderFeature.Downsampling;

namespace MyRenderPipeline.RenderPass.MyBlit
{
	public class MyBlitRenderPass : ScriptableRenderPass
	{
		private const string m_ProfilerTag = "My Copy Color";

		private static readonly int sampleOffset_ID = Shader.PropertyToID("_SampleOffset");
		private static readonly int cameraColorTexture_ID = Shader.PropertyToID("_CameraColorTexture");
		private static readonly int cameraOpaqueTexture_ID = Shader.PropertyToID("_CameraOpaqueTexture");


		private Material blitMaterial;
		private Downsampling downsampling;

		private RenderTargetIdentifier src_RTI;
		private RenderTargetIdentifier tempRT_RTI;

		public MyBlitRenderPass(Material blitMaterial, Downsampling downsampling)
		{
			this.blitMaterial = blitMaterial;
			//URP的获取方法  UniversalRenderPipeline.asset.opaqueDownsampling
			this.downsampling = downsampling;
			src_RTI = new RenderTargetIdentifier(cameraColorTexture_ID);
			tempRT_RTI = new RenderTargetIdentifier(cameraOpaqueTexture_ID);
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescripor)
		{
			RenderTextureDescriptor descriptor = cameraTextureDescripor;
			descriptor.msaaSamples = 1;
			descriptor.depthBufferBits = 0;
			if (downsampling == Downsampling._2xBilinear)
			{
				descriptor.width /= 2;
				descriptor.height /= 2;
			}
			else if (downsampling == Downsampling._4xBox || downsampling == Downsampling._4xBilinear)
			{
				descriptor.width /= 4;
				descriptor.height /= 4;
			}

			cmd.GetTemporaryRT(cameraOpaqueTexture_ID, descriptor,
				downsampling == Downsampling.None ? FilterMode.Point : FilterMode.Bilinear);
		}
		

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (blitMaterial == null)
			{
				Debug.LogErrorFormat(
					"Missing {0}. {1} render pass will not execute. Check for missing reference in the renderer resources.",
					blitMaterial, GetType().Name);
				return;
			}
			
			CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
			RenderTargetIdentifier opaqueColorRT = tempRT_RTI;

			switch (downsampling)
			{
				case Downsampling.None:
					Blit(cmd, src_RTI, opaqueColorRT);
					break;
				case Downsampling._2xBilinear:
					Blit(cmd, src_RTI, opaqueColorRT);
					break;
				case Downsampling._4xBox:
					blitMaterial.SetFloat(sampleOffset_ID, 2);
					Blit(cmd, src_RTI, opaqueColorRT, blitMaterial);
					break;
				case Downsampling._4xBilinear:
					Blit(cmd, src_RTI, opaqueColorRT);
					break;
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
		
		public override void FrameCleanup(CommandBuffer cmd)
		{
			if (cmd == null)
				throw new ArgumentNullException("cmd");

			cmd.ReleaseTemporaryRT(cameraOpaqueTexture_ID);
		}
	}
}