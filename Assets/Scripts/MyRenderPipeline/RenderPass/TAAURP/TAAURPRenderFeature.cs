using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	public class TAAURPRenderFeature : ScriptableRendererFeature
	{
		public Shader taaurpShader;

		private TAAURPProjectionRenderPass taaurpProjectionRenderPass;
		private TAAURPRenderPass taaurpRenderPass;

		private bool isNextFrame;
		private TAAURPData taaData;
		private Material taaurpMaterial;


		public override void Create()
		{
			if (taaurpMaterial != null && taaurpMaterial.shader != taaurpShader)
			{
				DestroyImmediate(taaurpMaterial);
			}

			if (taaurpShader == null)
			{
				Debug.LogError("Shader is null!");
				return;
			}

			taaurpMaterial = CoreUtils.CreateEngineMaterial(taaurpShader);

			taaurpProjectionRenderPass = new TAAURPProjectionRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
			};
			taaurpRenderPass = new TAAURPRenderPass()
			{
				renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
			};
			taaurpRenderPass.Init(taaurpMaterial);
			taaData = new TAAURPData();
			isNextFrame = false;
		}

		//这里默认只处理一个摄像机的   如果是多摄像机  添加一个dict<camera,taaData>维护
		//同时历史RT也要维护
		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			var camera = renderingData.cameraData.camera;

			//PreviewCamera 也会TAA
			if (camera.cameraType != CameraType.Game || camera.name.StartsWith("Preview"))
			{
				return;
			}

			if (taaurpRenderPass != null && renderingData.postProcessingEnabled)
			{
				var settings = VolumeManager.instance.stack.GetComponent<TAAURPPostProcess>();

				if (settings.IsActive())
				{
					UpdateTAAData(camera, settings);

					taaurpProjectionRenderPass.Setup(taaData.proOverride);
					renderer.EnqueuePass(taaurpProjectionRenderPass);

					taaurpRenderPass.Setup(settings, ref taaData);
					renderer.EnqueuePass(taaurpRenderPass);

					isNextFrame = true;
				}
				else
				{
					isNextFrame = false;
					taaurpRenderPass?.ClearRT();
				}
			}
			else
			{
				isNextFrame = false;
				taaurpRenderPass?.ClearRT();
			}
		}

		private void UpdateTAAData(Camera camera, TAAURPPostProcess settings)
		{
			Vector2 offset = TAAURPUtils.GenerateRandomOffset() * settings.spread.value;
			//连续的下一帧
			taaData.viewPrevious = isNextFrame ? taaData.viewCurrent : camera.worldToCameraMatrix;
			taaData.projPrevious = isNextFrame ? taaData.projCurrent : camera.projectionMatrix;

			taaData.proOverride =
				camera.orthographic
					? TAAURPUtils.GetJitteredOrthographicProjectionMatrix(camera, offset)
					: TAAURPUtils.GetJitteredPerspectiveProjectionMatrix(camera, offset);


			taaData.sampleOffset = new Vector2(offset.x / camera.scaledPixelWidth, offset.y / camera.scaledPixelHeight);

			taaData.viewCurrent = camera.worldToCameraMatrix;
			taaData.projCurrent = camera.projectionMatrix;//因为反算的时候 uv走的是 unjittered的
		}
	}
}