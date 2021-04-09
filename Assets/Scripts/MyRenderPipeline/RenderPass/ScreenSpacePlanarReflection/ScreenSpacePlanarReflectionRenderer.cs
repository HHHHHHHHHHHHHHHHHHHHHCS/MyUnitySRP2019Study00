using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.ScreenSpacePlanarReflection
{
	public class ScreenSpacePlanarReflectionRenderer : ScriptableRenderPass
	{
		private static readonly int sspr_ColorRT_pid = Shader.PropertyToID("_MobileSSPR_ColorRT");
		private static readonly int sspr_PackedDataRT_pid = Shader.PropertyToID("_MobileSSPR_PackedDataRT");
		private static readonly int sspr_PosWSyRT_pid = Shader.PropertyToID("_MobileSSPR_PosWSyRT");
		public static readonly int inverseViewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixInvVP");


		private RenderTargetIdentifier sspr_ColorRT_rti = new RenderTargetIdentifier(sspr_ColorRT_pid);
		private RenderTargetIdentifier sspr_PackedDataRT_rti = new RenderTargetIdentifier(sspr_PackedDataRT_pid);
		private RenderTargetIdentifier sspr_PosWSyRT_rti = new RenderTargetIdentifier(sspr_PosWSyRT_pid);

		private ShaderTagId lightMode_SSPR_sti = new ShaderTagId("MobileSSPR");


		private const int Shader_NumThread_X = 8; //must match compute shader's [numthread(x)]
		private const int Shader_NumThread_Y = 8; //must match compute shader's [numthread(y)]

		private ScreenSpacePlanarReflectionFeature settings;

		private ComputeShader cs;

		public ScreenSpacePlanarReflectionRenderer(ScreenSpacePlanarReflectionFeature _settings)
		{
			settings = _settings;
			cs = _settings.computeShader;
		}

		private int RTHeight => Mathf.CeilToInt(settings.rt_height / (float) Shader_NumThread_Y) * Shader_NumThread_Y;

		//有自适应屏幕高宽
		private int RTWidth => Mathf.CeilToInt(RTHeight * (Screen.width / (float) Screen.height) / Shader_NumThread_X) *
		                       Shader_NumThread_X;

		private bool ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve()
		{
			if (settings.enablePerPlatformAutoSafeGuard)
			{
#if UNITY_EDITOR
				return false; //PC / Mac must support the Non-Mobile path
#endif
				//force use MobilePathSinglePassColorRTDirectResolve, if RInt RT is not supported
				if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RInt))
					return true;
#if UNITY_ANDROID
                //- samsung galaxy A70(Adreno612) will fail if use RenderTextureFormat.RInt + InterlockedMin() in compute shader
                //- but Lenovo S5(Adreno506) is correct, WTF???
                //because behavior is different across android devices, we assume all android are not safe to use RenderTextureFormat.RInt + InterlockedMin() in compute shader
                return true;
#endif

#if UNITY_IOS
                //we don't know the answer now, need to build test
#endif
			}

			//let user decide if we still don't know the correct answer
			return !settings.shouldRemoveFlickerFinalControl;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			RenderTextureDescriptor rtd =
				new RenderTextureDescriptor(RTWidth, RTHeight, RenderTextureFormat.Default, 0, 0);

			rtd.sRGB = false; //不需要gamma
			rtd.enableRandomWrite = true; //using RWTexture2D in compute shader

			bool shouldUseHDRColorRT = settings.useHDR;
			if (cameraTextureDescriptor.colorFormat == RenderTextureFormat.ARGB32)
			{
				shouldUseHDRColorRT = false; //如果不是 hdrRT  就不需要HDR了
			}

			rtd.colorFormat =
				shouldUseHDRColorRT
					? RenderTextureFormat.ARGBHalf
					: RenderTextureFormat.ARGB32; //我们需要Alpha  通常LDR就足够了，忽略HDR是可以接受的反射
			cmd.GetTemporaryRT(sspr_ColorRT_pid, rtd);

			//packedData RT
			if (ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve())
			{
				//mobile 
				rtd.colorFormat = RenderTextureFormat.RFloat;
				cmd.GetTemporaryRT(sspr_PosWSyRT_pid, rtd);
			}
			else
			{
				//editor/PC 支持
				rtd.colorFormat = RenderTextureFormat.RInt;
				cmd.GetTemporaryRT(sspr_PackedDataRT_pid, rtd);
			}
		}

		//URP 7.5.3之后  cameraData.GetGPUProjectionMatrix() 进行y翻转
		public static void SetCameraInvVP(CommandBuffer cmd, ref CameraData cameraData)
		{
			Matrix4x4 viewMatrix = cameraData.camera.worldToCameraMatrix;
			Matrix4x4 projectionMatrix = cameraData.camera.projectionMatrix;

			Matrix4x4 viewAndProjectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, false) * viewMatrix;
			Matrix4x4 inverseViewProjection = Matrix4x4.Inverse(viewAndProjectionMatrix);
			cmd.SetGlobalMatrix(inverseViewAndProjectionMatrix, inverseViewProjection);
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			CommandBuffer cb = CommandBufferPool.Get("SSPR");

			int dispatchThreadGroupXCount = RTWidth / Shader_NumThread_X; //shader's numthreads x
			int dispatchThreadGroupYCount = RTHeight / Shader_NumThread_Y; //shader's numthreads y
			int dispatchThreadGroupZCount = 1;

			if (settings.shouldRenderSSPR)
			{
				//URP 升级的问题
				//这里是compute shader要采样世界空间  所以要用修改InvVP矩阵 为 no render to rt
				SetCameraInvVP(cb, ref renderingData.cameraData);

				cb.SetComputeVectorParam(cs, Shader.PropertyToID("_RTSize"), new Vector2(RTWidth, RTHeight));
				cb.SetComputeFloatParam(cs, Shader.PropertyToID("_HorizontalPlaneHeightWS"),
					settings.horizontalReflectionPlaneHeightWS);
				cb.SetComputeFloatParam(cs, Shader.PropertyToID("_FadeOutScreenBorderWidthVertical"),
					settings.fadeOutScreenBorderWidthVertical);
				cb.SetComputeFloatParam(cs, Shader.PropertyToID("_FadeOutScreenBorderWidthHorizontal"),
					settings.fadeOutScreenBorderWidthHorizontal);
				cb.SetComputeVectorParam(cs, Shader.PropertyToID("_CameraDirection"),
					renderingData.cameraData.camera.transform.forward);
				cb.SetComputeFloatParam(cs, Shader.PropertyToID("_ScreenLRStretchIntensity"),
					settings.screenLRStretchIntensity);
				cb.SetComputeFloatParam(cs, Shader.PropertyToID("_ScreenLRStretchThreshold"),
					settings.screenLRStretchThreshold);
				cb.SetComputeVectorParam(cs, Shader.PropertyToID("_FinalTintColor"), settings.tintColor);


				if (ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve())
				{
					//mobile
					int kernel_MobilePathSinglePassColorRTDirectResolve =
						cs.FindKernel("MobilePathSinglePassColorRTDirectResolve");
					cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve, "ColorRT",
						sspr_ColorRT_rti);
					cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve, "PosWSyRT",
						sspr_PosWSyRT_rti);
					cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve,
						"_CameraOpaqueTexture",
						new RenderTargetIdentifier("_CameraOpaqueTexture"));
					cb.SetComputeTextureParam(cs, kernel_MobilePathSinglePassColorRTDirectResolve,
						"_CameraDepthTexture",
						new RenderTargetIdentifier("_CameraDepthTexture"));
					cb.DispatchCompute(cs, kernel_MobilePathSinglePassColorRTDirectResolve, dispatchThreadGroupXCount,
						dispatchThreadGroupYCount, dispatchThreadGroupZCount);
				}
				else
				{
					//editor/pc

					//kernel NonMobilePathClear
					int kernel_NonMobilePathClear = cs.FindKernel("NonMobilePathClear");
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathClear, "HashRT", sspr_PackedDataRT_rti);
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathClear, "ColorRT", sspr_ColorRT_rti);
					cb.DispatchCompute(cs, kernel_NonMobilePathClear, dispatchThreadGroupXCount,
						dispatchThreadGroupYCount,
						dispatchThreadGroupZCount);

					//kernel NonMobilePathRenderHashRT
					int kernel_NonMobilePathRenderHashRT = cs.FindKernel("NonMobilePathRenderHashRT");
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathRenderHashRT, "HashRT", sspr_PackedDataRT_rti);
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathRenderHashRT, "_CameraDepthTexture",
						new RenderTargetIdentifier("_CameraDepthTexture"));
					cb.DispatchCompute(cs, kernel_NonMobilePathRenderHashRT, dispatchThreadGroupXCount,
						dispatchThreadGroupYCount, dispatchThreadGroupZCount);

					//resolve to ColorRT
					int kernel_NonMobilePathResolveColorRT = cs.FindKernel("NonMobilePathResolveColorRT");
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "HashRT", sspr_PackedDataRT_rti);
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "ColorRT", sspr_ColorRT_rti);
					cb.SetComputeTextureParam(cs, kernel_NonMobilePathResolveColorRT, "_CameraOpaqueTexture",
						new RenderTargetIdentifier("_CameraOpaqueTexture"));
					cb.DispatchCompute(cs, kernel_NonMobilePathResolveColorRT, dispatchThreadGroupXCount,
						dispatchThreadGroupYCount, dispatchThreadGroupZCount);
				}

				if (settings.applyFillHoleFix)
				{
					int kernel_FillHoles = cs.FindKernel("FillHoles");
					cb.SetComputeTextureParam(cs, kernel_FillHoles, "ColorRT", sspr_ColorRT_rti);
					cb.SetComputeTextureParam(cs, kernel_FillHoles, "PackedDataRT", sspr_PackedDataRT_rti);
					//半分辨率
					cb.DispatchCompute(cs, kernel_FillHoles, Mathf.CeilToInt(dispatchThreadGroupXCount / 2f),
						Mathf.CeilToInt(dispatchThreadGroupYCount / 2f), dispatchThreadGroupZCount);
				}

				//发送到全局
				cb.SetGlobalTexture(sspr_ColorRT_pid, sspr_ColorRT_rti);
				cb.EnableShaderKeyword("_MobileSSPR");

				//复原矩阵
				cb.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix,renderingData.cameraData.camera.projectionMatrix);
				//ScriptableRenderer.SetCameraMatrices(cb, ref renderingData.cameraData, true);
			}
			else
			{
				cb.DisableShaderKeyword("_MobileSSPR");
			}

			context.ExecuteCommandBuffer(cb);
			CommandBufferPool.Release(cb);

			//draw planar reflection 
			DrawingSettings drawingSettings =
				CreateDrawingSettings(lightMode_SSPR_sti, ref renderingData, SortingCriteria.CommonOpaque);
			FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);
			context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			cmd.ReleaseTemporaryRT(sspr_ColorRT_pid);

			if (ShouldUseSinglePassUnsafeAllowFlickeringDirectResolve())
				cmd.ReleaseTemporaryRT(sspr_PosWSyRT_pid);
			else
				cmd.ReleaseTemporaryRT(sspr_PackedDataRT_pid);
		}
	}
}