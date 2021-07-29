using System.Collections.Generic;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
	public class HiZSRPDepthPass : ScriptableRenderPass
	{
		private const string k_profilingTag = "HiZDepth";
		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_profilingTag);


		private Shader blitShader;
		private Material blitMaterial;

		//也可以用 RenderTargetHandle  只不过会一直返回new RenderTargetIdentifier
		private RenderTextureDescriptor textureDescriptor;
		private RenderTexture depthRT;
		private float width, height;

		//因为只要Game 的 正确就够了  drawInstanceIndirection 也是依赖于Game视图的
		public static RenderTexture DepthTexture { get; private set; }

		public HiZSRPDepthPass()
		{
			blitShader = Shader.Find("MyRP/HiZ/HiZCopyDepth");
			blitMaterial = new Material(blitShader);

			width = height = -1;
		}


		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			textureDescriptor = cameraTextureDescriptor;
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cmd = CommandBufferPool.Get(k_profilingTag);
			using (new ProfilingScope(cmd, profilingSampler))
			{
				if (width != textureDescriptor.width || height != textureDescriptor.height)
				{
					if (width == -1 || height == -1)
					{
						RenderTexture.Destroy(depthRT);
					}

					width = textureDescriptor.width;
					height = textureDescriptor.height;
					depthRT = new RenderTexture(textureDescriptor.width, textureDescriptor.height, 0,
						RenderTextureFormat.RFloat, Mathf.CeilToInt(Mathf.Log(width, 2)))
					{
						name = "HiZDepth",
						useMipMap = true,
						autoGenerateMips = false
					};
					DepthTexture = depthRT;
					depthRT.Create();
				}

				cmd.SetRenderTarget(depthRT);
				cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, blitMaterial);
				cmd.SetRenderTarget(renderingData.cameraData.targetTexture); //复原
				//正常mipmap要自己写生成的取周围点的做max/min  但是这里我偷懒了
				cmd.GenerateMips(depthRT);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}