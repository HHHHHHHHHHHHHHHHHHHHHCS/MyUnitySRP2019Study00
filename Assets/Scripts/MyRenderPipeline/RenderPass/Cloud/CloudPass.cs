using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud
{
	public class CloudPass : MyRenderPassAsset
	{
		public bool drawFullScreen = false;
		public Material material;
		public ComputeShader curlNoiseMotionComputeShader;
		public RenderTexture curlNoiseTexture;

		public override MyRenderPass CreateRenderPass()
		{
			return new CloudPassRenderer(this);
		}
	}

	public class CloudPassRenderer : MyRenderPassRenderer<CloudPass>
	{
		private Mesh screenMesh;
		private CurlNoiseMotionRenderer CurlNoiseMotionRenderer;

		public CloudPassRenderer(CloudPass asset) : base(asset)
		{
			screenMesh = RenderUtils.GenerateFullScreenQuad();
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			base.Setup(context, ref renderingData);
			//TODO:
		}
	}
}