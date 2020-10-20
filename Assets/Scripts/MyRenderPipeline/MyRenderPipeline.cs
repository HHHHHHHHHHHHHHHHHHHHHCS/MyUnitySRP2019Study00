using System.Collections.Generic;
using MyRenderPipeline;
using MyRenderPipeline.RenderPass;
using MyRenderPipeline.RenderPass.Common;
using MyRenderPipeline.RenderPass.Shadow;
using MyRenderPipeline.Shadow;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline
{
	public class MyRenderPipeline : UnityEngine.Rendering.RenderPipeline
	{
		private MyRenderPipelineAsset settings { get; set; }
		private List<MyRenderPass> renderPassQueue = new List<MyRenderPass>();
		private List<MyUserPass> globalUserPasses = new List<MyUserPass>();

		private int colorTarget;
		private int depthTarget;
		private bool rtCreated = false;
		private int frameID = 0;
		private DoubleBuffer<Vector2> projectionJitter = new DoubleBuffer<Vector2>((_) => new Vector2(0.5f, 0.5f));

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

			frameID++;
			projectionJitter.Flip();
		}

		protected virtual void RenderCamera(ScriptableRenderContext context, Camera camera)
		{
			camera.TryGetCullingParameters(out var cullingParameters);

			var cmd = CommandBufferPool.Get(camera.name);

			cmd.Clear();

			//Emit UI
			if (camera.cameraType == CameraType.SceneView)
				ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);

			var cullResults = context.Cull(ref cullingParameters);

			var projectionMat = camera.projectionMatrix;
			var jitteredProjectionMat = projectionMat;
			//隔帧数抖动
			jitteredProjectionMat.m02 += (projectionJitter.Current.x * 2 - 1) / camera.pixelWidth;
			jitteredProjectionMat.m12 += (projectionJitter.Current.y * 2 - 1) / camera.pixelHeight;

			var renderingData = new MyRenderingData()
			{
				camera = camera,
				cullResults = cullResults,
				colorTarget = BuiltinRenderTextureType.CameraTarget,
				depthTarget = BuiltinRenderTextureType.CameraTarget,
				colorBufferFormat = RenderTextureFormat.Default,
				shadowMapData = new Dictionary<Light, MyShadowMapData>(),
				frameID = frameID,
				discardFrameBuffer = true,
				viewMatrix = camera.worldToCameraMatrix,
				projectionMatrix = projectionMat,
				jitteredProjectionMatrix = jitteredProjectionMat,
				projectionJitter = projectionJitter.Current,
				nextProjectionJitter = new Vector2(0.5f, 0.5f),
				resolutionScale = settings.ResolutionScale
			};

			Setup(context, ref renderingData);
			context.SetupCameraProperties(camera, false);

			InitRenderQueue(camera);
			SetupLight(ref renderingData);


			cmd.SetRenderTarget(colorTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, depthTarget,
				RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			cmd.ClearRenderTarget(true, true, Color.black, 1);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();

			context.DrawSkybox(camera);

			foreach (var pass in renderPassQueue)
			{
				pass.Setup(context, ref renderingData);
				pass.Render(context, ref renderingData);
			}

			foreach (var pass in globalUserPasses)
			{
				pass.Setup(context, ref renderingData);
				pass.Render(context, ref renderingData);
			}

			var userPasses = camera.GetComponents<MyUserPass>();
			foreach (var pass in userPasses)
			{
				if (pass.global)
				{
					continue;
				}

				pass.Setup(context, ref renderingData);
				pass.Render(context, ref renderingData);
			}

			cmd.SetViewProjectionMatrices(renderingData.viewMatrix, renderingData.projectionMatrix);
			cmd.Blit(renderingData.colorTarget, BuiltinRenderTextureType.CameraTarget);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();

			if (camera.cameraType == CameraType.SceneView)
			{
				//draw Gizmos 
				context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
				context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
			}

			foreach (var pass in renderPassQueue)
			{
				pass.Cleanup(context, ref renderingData);
			}

			foreach (var pass in globalUserPasses)
			{
				pass.Cleanup(context, ref renderingData);
			}

			foreach (var pass in userPasses)
			{
				pass.Cleanup(context, ref renderingData);
			}

			Cleanup(context, ref renderingData);

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
			context.Submit();

			projectionJitter.Next = renderingData.nextProjectionJitter;
		}

		private void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			if (!rtCreated)
			{
				rtCreated = true;
				var camera = renderingData.camera;
				var cmd = CommandBufferPool.Get();
				renderingData.colorBufferFormat =
					settings.HDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

				colorTarget = Shader.PropertyToID("_ColorTarget");
				depthTarget = Shader.PropertyToID("_DepthTarget");
				cmd.GetTemporaryRT(colorTarget, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Point,
					renderingData.colorBufferFormat);
				cmd.GetTemporaryRT(depthTarget, camera.pixelWidth, camera.pixelHeight, 32, FilterMode.Point,
					RenderTextureFormat.Depth);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				CommandBufferPool.Release(cmd);
			}

			renderingData.colorTarget = colorTarget;
			renderingData.depthTarget = depthTarget;
		}

		private void Cleanup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get();
			cmd.ReleaseTemporaryRT(colorTarget);
			cmd.ReleaseTemporaryRT(depthTarget);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
			rtCreated = false;
		}

		private void SetupLight(ref MyRenderingData renderingData)
		{
			renderingData.lights = renderingData.cullResults.visibleLights;
		}

		private void InitRenderQueue(Camera camera)
		{
			renderPassQueue.Clear();
			foreach (var renderPassAsset in settings.RenderPasses)
			{
				if (renderPassAsset)
				{
					var pass = renderPassAsset.GetRenderPass(camera);
					pass.InternalSetup();
					renderPassQueue.Add(pass);
				}
			}
		}
	}
}