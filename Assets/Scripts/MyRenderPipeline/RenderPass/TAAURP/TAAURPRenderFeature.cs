using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	public class TAAURPRenderFeature : ScriptableRendererFeature
	{
		private Camera taaCamera;
		private TAAURPData taaData;
		private TAAURPRenderPass taaurpRenderPass;

		private Matrix4x4 preViewView;
		private Matrix4x4 preViewProj;
		
		public override void Create()
		{
			taaurpRenderPass = new TAAURPRenderPass();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			var camera = renderingData.cameraData.camera;

			if (renderingData.postProcessingEnabled && camera.cameraType == CameraType.Game)
			{
				var settings = VolumeManager.instance.stack.GetComponent<TAAURPPostProcess>();
				
			}
		}

		private void UpdateTAAData(Camera camera, TAAURPData data)
		{
			
		}
	}
}