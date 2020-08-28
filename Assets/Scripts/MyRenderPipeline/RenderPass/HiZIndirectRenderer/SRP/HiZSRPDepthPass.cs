using System.Collections.Generic;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
	public class HiZSRPDepthPass : ScriptableRenderPass
	{
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
			if (!Application.isPlaying)
			{
				return;
			}

			if (renderingData.cameraData.camera.cameraType != CameraType.Game)
			{
				return;
			}


			CommandBuffer cmd = CommandBufferPool.Get("HiZDepth");
			using (new ProfilingSample(cmd, "HiZDepth"))
			{
				cmd.Clear();

				if (width != textureDescriptor.width || height != textureDescriptor.height)
				{
					if (width == -1 || height == -1)
					{
						RenderTexture.Destroy(depthRT);
					}

					width = textureDescriptor.width;
					height = textureDescriptor.height;
					depthRT = new RenderTexture(textureDescriptor.width, textureDescriptor.height, 32,
						RenderTextureFormat.RFloat,
						-1)
					{
						name = "HiZDepth"
					};
					DepthTexture = depthRT;
				}

				cmd.SetRenderTarget(depthRT);
				cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, blitMaterial);
				cmd.SetRenderTarget(renderingData.cameraData.targetTexture); //复原
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}