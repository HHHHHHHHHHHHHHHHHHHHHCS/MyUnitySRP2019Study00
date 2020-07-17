using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline
{
	public abstract class MyRenderPassAsset : ScriptableObject
	{
		private Dictionary<Camera, MyRenderPass> perCameraPass = new Dictionary<Camera, MyRenderPass>();

		public abstract MyRenderPass CreateRenderPass();

		public MyRenderPass GetRenderPass(Camera camera)
		{
			if (!perCameraPass.ContainsKey(camera))
				perCameraPass[camera] = CreateRenderPass();
			return perCameraPass[camera];
		}
	}
}