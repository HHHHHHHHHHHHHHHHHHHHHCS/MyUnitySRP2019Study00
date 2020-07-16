using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RenderPipeline
{
	public abstract class RenderPassAsset : ScriptableObject
	{
		private Dictionary<Camera, RenderPass> perCameraPass = new Dictionary<Camera, RenderPass>();

		public abstract RenderPass CreateRenderPass();

		public RenderPass GetRenderPass(Camera camera)
		{
			if (!perCameraPass.ContainsKey(camera))
				perCameraPass[camera] = CreateRenderPass();
			return perCameraPass[camera];
		}
	}
}