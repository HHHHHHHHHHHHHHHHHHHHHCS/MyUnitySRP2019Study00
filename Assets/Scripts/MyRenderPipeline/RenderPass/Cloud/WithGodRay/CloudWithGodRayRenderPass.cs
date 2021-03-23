using MyRenderPipeline.RenderPass.Cloud.ImageEffect;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.WithGodRay
{
	//https://zhuanlan.zhihu.com/p/248965902
	public class CloudWithGodRayRenderPass : ScriptableRenderPass
	{
		private const string k_CloudWithGodRayPass = "CloudWithGodRayPass";

		private static readonly int BoundsMin_ID = Shader.PropertyToID("_BoundsMin");
		private static readonly int BoundsMax_ID = Shader.PropertyToID("_BoundsMax");
		private static readonly int NoiseTex_ID = Shader.PropertyToID("_NoiseTex");
		private static readonly int NoiseDetail3D_ID = Shader.PropertyToID("_NoiseDetail3D");
		private static readonly int WeatherMap_ID = Shader.PropertyToID("_WeatherMap");
		private static readonly int MaskNoise_ID = Shader.PropertyToID("_MaskNoise");
		private static readonly int BlueNoiseCoords_ID = Shader.PropertyToID("_BlueNoiseCoords");
		private static readonly int BlueNoise_ID = Shader.PropertyToID("_BlueNoise");
		private static readonly int ShapeTiling_ID = Shader.PropertyToID("_ShapeTiling");
		private static readonly int DetailTiling_ID = Shader.PropertyToID("_DetailTiling");
		private static readonly int Step_ID = Shader.PropertyToID("_Step");
		private static readonly int RayStep_ID = Shader.PropertyToID("_RayStep");
		private static readonly int DensityOffset_ID = Shader.PropertyToID("_DensityOffset");
		private static readonly int DensityMultiplier_ID = Shader.PropertyToID("_DensityMultiplier");
		private static readonly int ColA_ID = Shader.PropertyToID("_ColA");
		private static readonly int ColB_ID = Shader.PropertyToID("_ColB");
		private static readonly int ColorOffset1_ID = Shader.PropertyToID("_ColorOffset1");
		private static readonly int ColorOffset2_ID = Shader.PropertyToID("_ColorOffset2");
		private static readonly int LightAbsorptionTowardSun_ID = Shader.PropertyToID("_LightAbsorptionTowardSun");

		private static readonly int LightAbsorptionThroughCloud_ID =
			Shader.PropertyToID("_LightAbsorptionThroughCloud");

		private static readonly int RayOffsetStrength_ID = Shader.PropertyToID("_RayOffsetStrength");
		private static readonly int PhaseParams_ID = Shader.PropertyToID("_PhaseParams");
		private static readonly int XY_Speed_ZW_Warp_ID = Shader.PropertyToID("_XY_Speed_ZW_Warp");
		private static readonly int ShapeNoiseWeights_ID = Shader.PropertyToID("_ShapeNoiseWeights");
		private static readonly int HeightWeights_ID = Shader.PropertyToID("_HeightWeights");
		private static readonly int DetailWeights_ID = Shader.PropertyToID("_DetailWeights");
		private static readonly int DetailNoiseWeight_ID = Shader.PropertyToID("_DetailNoiseWeight");
		private static readonly int DetailNoiseWeights_ID = Shader.PropertyToID("_DetailNoiseWeights");


		private static readonly int DownsampleDepthTex_ID = Shader.PropertyToID("_DownsampleDepthTex");
		private static readonly int DownsampleColorTex_ID = Shader.PropertyToID("_DownsampleColorTex");


		private static readonly RenderTargetIdentifier DownsampleDepthTex_RTI =
			new RenderTargetIdentifier(DownsampleDepthTex_ID);

		private static readonly RenderTargetIdentifier DownsampleColorTex_RTI =
			new RenderTargetIdentifier(DownsampleColorTex_ID);

		private static readonly RenderTargetIdentifier CameraColorTex_RTI =
			new RenderTargetIdentifier("_CameraColorTexture");


		private Texture3D shapeTexture;
		private Texture3D detailTexture;
		private Texture2D weatherMap;
		private Texture2D blueNoise;
		private Texture2D maskNoise;

		private Material cloudMaterial;

		private ContainerVis containerVis;
		private CloudWithGodRayPostProcess cloudSettings;
		private RenderTextureDescriptor cameraTextureDescriptor;


		public void Init(Material cloudMaterial, Texture3D shapeTexture, Texture3D detailTexture, Texture2D weatherMap,
			Texture2D blueNoise, Texture2D maskNoise)
		{
			this.cloudMaterial = cloudMaterial;
			this.shapeTexture = shapeTexture;
			this.detailTexture = detailTexture;
			this.weatherMap = weatherMap;
			this.blueNoise = blueNoise;
			this.maskNoise = maskNoise;
		}

		public void Setup(CloudWithGodRayPostProcess cloudSettings)
		{
			if (containerVis == null)
			{
				containerVis = Object.FindObjectOfType<ContainerVis>();
			}

			this.cloudSettings = cloudSettings;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			this.cameraTextureDescriptor = cameraTextureDescriptor;
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (containerVis == null)
			{
				return;
			}

			var cmd = CommandBufferPool.Get(k_CloudWithGodRayPass);
			var sampler = new ProfilingSampler(k_CloudWithGodRayPass);

			using (new ProfilingScope(cmd, sampler))
			{
				var pos = containerVis.transform.position;
				var halfSize = containerVis.transform.lossyScale / 2.0f;
				cloudMaterial.SetVector(BoundsMin_ID, value: pos - halfSize);
				cloudMaterial.SetVector(BoundsMax_ID, pos + halfSize);

				cloudMaterial.SetTexture(NoiseTex_ID, shapeTexture);
				cloudMaterial.SetTexture(NoiseDetail3D_ID, detailTexture);
				cloudMaterial.SetTexture(WeatherMap_ID, weatherMap);
				cloudMaterial.SetTexture(MaskNoise_ID, maskNoise);
				cloudMaterial.SetTexture(BlueNoise_ID, blueNoise);

				Vector4 screenUV = new Vector4(1, 1, 0, 0);
				if (blueNoise != null)
				{
					screenUV.x = (float) cameraTextureDescriptor.width / blueNoise.width;
					screenUV.y = (float) cameraTextureDescriptor.height / blueNoise.height;
				}

				cloudMaterial.SetVector(BlueNoiseCoords_ID, screenUV);


				cloudMaterial.SetFloat(ShapeTiling_ID, cloudSettings.shapeTiling.value);
				cloudMaterial.SetFloat(DetailTiling_ID, cloudSettings.detailTiling.value);

				cloudMaterial.SetFloat(Step_ID, cloudSettings.step.value);
				cloudMaterial.SetFloat(RayStep_ID, cloudSettings.rayStep.value);

				cloudMaterial.SetFloat(DensityOffset_ID, cloudSettings.densityOffset.value);
				cloudMaterial.SetFloat(DensityMultiplier_ID, cloudSettings.densityMultiplier.value);


				cloudMaterial.SetColor(ColA_ID, cloudSettings.colA.value);
				cloudMaterial.SetColor(ColB_ID, cloudSettings.colB.value);
				cloudMaterial.SetFloat(ColorOffset1_ID, cloudSettings.colorOffset1.value);
				cloudMaterial.SetFloat(ColorOffset2_ID, cloudSettings.colorOffset2.value);
				cloudMaterial.SetFloat(LightAbsorptionTowardSun_ID,
					cloudSettings.lightAbsorptionTowardSun.value);
				cloudMaterial.SetFloat(LightAbsorptionThroughCloud_ID,
					cloudSettings.lightAbsorptionThroughCloud.value);


				cloudMaterial.SetFloat(RayOffsetStrength_ID, cloudSettings.rayOffsetStrength.value);
				cloudMaterial.SetVector(PhaseParams_ID, cloudSettings.phaseParams.value);
				cloudMaterial.SetVector(XY_Speed_ZW_Warp_ID, cloudSettings.xy_Speed_zw_Warp.value);

				cloudMaterial.SetVector(ShapeNoiseWeights_ID, cloudSettings.shapeNoiseWeights.value);
				cloudMaterial.SetFloat(HeightWeights_ID, cloudSettings.heightWeights.value);


				cloudMaterial.SetFloat(DetailWeights_ID, cloudSettings.detailWeights.value);
				cloudMaterial.SetFloat(DetailNoiseWeight_ID, cloudSettings.detailNoiseWeight.value);
				cloudMaterial.SetVector(DetailNoiseWeights_ID, cloudSettings.detailNoiseWeights.value);

				//降深度采样
				cmd.GetTemporaryRT(DownsampleDepthTex_ID
					, cameraTextureDescriptor.width / cloudSettings.downsample.value
					, cameraTextureDescriptor.height / cloudSettings.downsample.value
					, 0, FilterMode.Point, RenderTextureFormat.R16);
				cmd.SetRenderTarget(DownsampleDepthTex_RTI, RenderBufferLoadAction.DontCare,
					RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, cloudMaterial, null, 1);
				cmd.SetGlobalTexture(DownsampleDepthTex_ID, DownsampleDepthTex_RTI);

				//降cloud分辨率 并使用第1个pass 渲染云
				cmd.GetTemporaryRT(DownsampleColorTex_ID
					, cameraTextureDescriptor.width / cloudSettings.downsample.value
					, cameraTextureDescriptor.height / cloudSettings.downsample.value
					, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
				cmd.SetRenderTarget(DownsampleColorTex_RTI, RenderBufferLoadAction.DontCare,
					RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, cloudMaterial, null, 0);
				//降分辨率后的云设置回_DownsampleColor
				cmd.SetGlobalTexture(DownsampleColorTex_ID, DownsampleColorTex_RTI);

				//使用第2个Pass 合成
				cmd.SetRenderTarget(CameraColorTex_RTI);

				CoreUtils.DrawFullScreen(cmd, cloudMaterial, null, 2);


				cmd.ReleaseTemporaryRT(DownsampleDepthTex_ID);
				cmd.ReleaseTemporaryRT(DownsampleColorTex_ID);
			}

			context.ExecuteCommandBuffer(cmd);
			context.Submit();
			CommandBufferPool.Release(cmd);
		}
	}
}

/*
 		private static readonly int InverseProjectionMatrix_ID = Shader.PropertyToID("_InverseProjectionMatrix");
		private static readonly int InverseViewMatrix_ID = Shader.PropertyToID("_InverseViewMatrix");
		private static readonly int RayRange_ID = Shader.PropertyToID("_RayRange");
		private static readonly int RayIntensity_ID = Shader.PropertyToID("_RayIntensity");
		private static readonly int RayPower_ID = Shader.PropertyToID("_RayPower");
		private static readonly int LightThreshold_ID = Shader.PropertyToID("_LightThreshold");
		private static readonly int QualityStep_ID = Shader.PropertyToID("_QualityStep");
		private static readonly int OffsetUV_ID = Shader.PropertyToID("_OffsetUV");
		private static readonly int BoxBlur_ID = Shader.PropertyToID("_BoxBlur");
		private static readonly int LightColor_ID = Shader.PropertyToID("_LightColor");
		private static readonly int LightViewPos_ID = Shader.PropertyToID("_LightViewPos");
		private static readonly int LightDir_ID = Shader.PropertyToID("_LightDir");
		private static readonly int CamDir_ID = Shader.PropertyToID("_CamDir");
		private static readonly int DownsampleTemp_ID = Shader.PropertyToID("_DownsampleTemp");
		private static readonly int LowResTexture_ID = Shader.PropertyToID("_LowResTexture");


 */