using UnityEngine;
using UnityEngine.Rendering.Universal;

//Copy by https://github.com/ColinLeung-NiloCat/UnityURP-MobileScreenSpacePlanarReflection
namespace MyRenderPipeline.RenderPass.ScreenSpacePlanarReflection
{
	public class ScreenSpacePlanarReflectionFeature : ScriptableRendererFeature
	{
		public static ScreenSpacePlanarReflectionFeature instance;
		
		public ComputeShader computeShader;

		[Header("Settings")] public bool shouldRenderSSPR = true;

		//故意抬高一点避免ZFight
		public float horizontalReflectionPlaneHeightWS = 0.01f;
		[Range(0.01f, 1f)] public float fadeOutScreenBorderWidthVertical = 0.25f;
		[Range(0.01f, 1f)] public float fadeOutScreenBorderWidthHorizontal = 0.35f;
		[Range(0, 8f)] public float screenLRStretchIntensity = 4;
		[Range(-1f, 1f)] public float screenLRStretchThreshold = 0.7f;
		[ColorUsage(true, true)] public Color tintColor = Color.white;

		//////////////////////////////////////////////////////////////////////////////////
		[Header("Performance Settings")]
		[Range(128, 1024)]
		[Tooltip("set to 512 or below for better performance, if visual quality lost is acceptable")]
		public int rt_height = 512; //分辨率

		[Tooltip("can set to false for better performance, if visual quality lost is acceptable")]
		public bool useHDR = true; //HDR

		[Tooltip("can set to false for better performance, if visual quality lost is acceptable")]
		public bool applyFillHoleFix = true; //填充小孔

		[Tooltip("can set to false for better performance, if flickering is acceptable")]
		public bool shouldRemoveFlickerFinalControl = true; //接受闪烁

		//////////////////////////////////////////////////////////////////////////////////
		[Header("Danger Zone")] [Tooltip("You should always turn this on, unless you want to debug")]
		public bool enablePerPlatformAutoSafeGuard = true; //平台RT检测 , 是否能启用Flicker


		private ScreenSpacePlanarReflectionRenderer rendererPass;

		public override void Create()
		{
			instance = this;
			return;
			rendererPass = new ScreenSpacePlanarReflectionRenderer(this);

			rendererPass.renderPassEvent =
				RenderPassEvent.AfterRenderingTransparents; //必须等 _CameraOpaqueTexture 和 _CameraDepthTexture 有内容了
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			return;
			renderer.EnqueuePass(rendererPass);
		}
	}
}