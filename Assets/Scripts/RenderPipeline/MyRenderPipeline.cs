using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RenderPipeline
{
	public class MyRenderPipeline : UnityEngine.Rendering.RenderPipeline
	{
		private MyRenderPipelineAsset settings { get; set; }
		private List<RenderPass> renderPassQueue = new List<RenderPass>();
		private List<UserPass> globalUserPasses = new List<UserPass>();

		private int colorTarget;
		private int depthTarget;
		private bool rtCreated = false;
		private int frameID = 0;

		public MyRenderPipeline(MyRenderPipelineAsset asset)
		{
			settings = asset;

			Shader.globalRenderPipeline = "MyRenderPipeline";
		}

		protected override void Render(ScriptableRenderContext context, Camera[] cameras)
		{
			BeginFrameRendering(context, cameras);

			foreach (var camera in cameras)
			{
				BeginCameraRendering(context, camera);

				RenderCamera(context, camera);

				EndCameraRendering(context, camera);
			}


			EndFrameRendering(context, cameras);
		}

		protected virtual void RenderCamera(ScriptableRenderContext context, Camera camera)
		{
		}
	}
}