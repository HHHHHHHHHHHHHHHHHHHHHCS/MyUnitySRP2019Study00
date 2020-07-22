using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass
{
	[CreateAssetMenu(fileName = "VelocityPass", menuName = "MyRP/RenderPass/VelocityPass")]
	public class VelocityPass : MyRenderPassAsset
	{
		public override MyRenderPass CreateRenderPass()
		{
			return new VelocityPassRenderer(this);
		}
	}

	public class VelocityPassRenderer : MyRenderPassRenderer<VelocityPass>
	{
		private enum ShaderPass : int
		{
			// OpaqueVelocity = 0,
			StaticVelocity = 0,
		}

		private static readonly ShaderTagId VelocityPassName = new ShaderTagId("MotionVectors");
		private const string ShaderName = "MyRP/VelocityBuffer";
		private int velocityBuffer;
		private Matrix4x4 previousGPUVPMatrix;
		private Vector2 previousJitterOffset;


		public VelocityPassRenderer(VelocityPass asset) : base(asset)
		{
		}

		protected override void Init()
		{
			velocityBuffer = Shader.PropertyToID("_VelocityBuffer");
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get();

			renderingData.camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

			cmd.GetTemporaryRT(velocityBuffer, renderingData.resolutionX, renderingData.resolutionY, 32,
				FilterMode.Point, RenderTextureFormat.RGFloat);
			if (renderingData.frameID == 0)
			{
				previousGPUVPMatrix = SaveGPUViewProjection(renderingData);
			}

			renderingData.velocityBuffer = velocityBuffer;

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		public override void Cleanup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get();
			cmd.ReleaseTemporaryRT(velocityBuffer);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var camera = renderingData.camera;
			var cmd = CommandBufferPool.Get("Velocity Pass");

			using (new ProfilingSample(cmd, "Velocity Pass"))
			{
				cmd.SetGlobalMatrix("_PreviousGPUViewProjection", previousGPUVPMatrix);
				cmd.SetGlobalTexture("_CameraDepthTex", renderingData.depthTarget);
				cmd.SetGlobalVector("_PreviousJitterOffset", previousJitterOffset);
				var jitterOffset = renderingData.projectionJitter - new Vector2(0.5f, 0.5f);
				cmd.SetGlobalVector("_CurrentJitterOffset", jitterOffset);

				cmd.SetCameraParams(renderingData.camera, false);
				cmd.SetViewProjectionMatrices(renderingData.viewMatrix, renderingData.jitteredProjectionMatrix);

				cmd.SetRenderTarget(velocityBuffer);
				cmd.ClearRenderTarget(true, true, Color.black);

				cmd.BlitFullScreen(BuiltinRenderTextureType.None, velocityBuffer, ShaderPool.Get(ShaderName),
					(int) ShaderPass.StaticVelocity);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

				FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
				SortingSettings sortingSettings = new SortingSettings(renderingData.camera)
				{
					criteria = SortingCriteria.CommonOpaque
				};
				DrawingSettings drawingSettings = new DrawingSettings(VelocityPassName, sortingSettings)
				{
					enableDynamicBatching = false,
					enableInstancing = true,
					perObjectData = PerObjectData.MotionVectors,
				};

				RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Nothing);

				context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
					ref stateBlock);
			}
			
			previousGPUVPMatrix = SaveGPUViewProjection(renderingData);
			previousJitterOffset = renderingData.projectionJitter - new Vector2(.5f, .5f);
			
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}


		Matrix4x4 SaveGPUViewProjection(MyRenderingData renderingData)
			=> GL.GetGPUProjectionMatrix(renderingData.jitteredProjectionMatrix, false) * renderingData.viewMatrix;
	}
}