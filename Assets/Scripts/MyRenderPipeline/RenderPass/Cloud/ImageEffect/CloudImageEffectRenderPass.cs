using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class CloudImageEffectRenderPass : ScriptableRenderPass
	{
		private const string k_CloudImageEffectPass = "CloudImageEffectPass";


		private static readonly int NoiseTex_ID = Shader.PropertyToID("_NoiseTex");
		private static readonly int DetailNoiseTex_ID = Shader.PropertyToID("_DetailNoiseTex");
		private static readonly int BlueNoise_ID = Shader.PropertyToID("_BlueNoise");
		private static readonly int WeatherMap_ID = Shader.PropertyToID("_WeatherMap");
		private static readonly int Scale_ID = Shader.PropertyToID("_Scale");
		private static readonly int DensityMultiplier_ID = Shader.PropertyToID("_DensityMultiplier");
		private static readonly int DensityOffset_ID = Shader.PropertyToID("_DensityOffset");

		private static readonly int LightAbsorptionThroughCloud_ID =
			Shader.PropertyToID("_LightAbsorptionThroughCloud");

		private static readonly int LightAbsorptionTowardSun_ID = Shader.PropertyToID("_LightAbsorptionTowardSun");
		private static readonly int DarknessThreshold_ID = Shader.PropertyToID("_DarknessThreshold");
		private static readonly int Params_ID = Shader.PropertyToID("_Params");
		private static readonly int RayOffsetStrength_ID = Shader.PropertyToID("_RayOffsetStrength");
		private static readonly int DetailNoiseScale_ID = Shader.PropertyToID("_DetailNoiseScale");
		private static readonly int DetailNoiseWeight_ID = Shader.PropertyToID("_DetailNoiseWeight");
		private static readonly int ShapeOffset_ID = Shader.PropertyToID("_ShapeOffset");
		private static readonly int DetailOffset_ID = Shader.PropertyToID("_DetailOffset");
		private static readonly int DetailWeights_ID = Shader.PropertyToID("_DetailWeights");
		private static readonly int ShapeNoiseWeights_ID = Shader.PropertyToID("_ShapeNoiseWeights");
		private static readonly int PhaseParams_ID = Shader.PropertyToID("_PhaseParams");
		private static readonly int BoundsMin_ID = Shader.PropertyToID("_BoundsMin");
		private static readonly int BoundsMax_ID = Shader.PropertyToID("_BoundsMax");
		private static readonly int NumStepsLight_ID = Shader.PropertyToID("_NumStepsLight");
		private static readonly int MapSize_ID = Shader.PropertyToID("_MapSize");
		private static readonly int TimeScale_ID = Shader.PropertyToID("_TimeScale");
		private static readonly int BaseSpeed_ID = Shader.PropertyToID("_BaseSpeed");
		private static readonly int DetailSpeed_ID = Shader.PropertyToID("_DetailSpeed");
		private static readonly int ColA_ID = Shader.PropertyToID("_ColA");
		private static readonly int ColB_ID = Shader.PropertyToID("_ColB");
		private static readonly int DebugViewMode_ID = Shader.PropertyToID("_DebugViewMode");
		private static readonly int DebugNoiseSliceDepth_ID = Shader.PropertyToID("_DebugNoiseSliceDepth");
		private static readonly int DebugTileAmount_ID = Shader.PropertyToID("_DebugTileAmount");
		private static readonly int ViewerSize_ID = Shader.PropertyToID("_ViewerSize");
		private static readonly int DebugChannelWeight_ID = Shader.PropertyToID("_DebugChannelWeight");
		private static readonly int DebugGreyscale_ID = Shader.PropertyToID("_DebugGreyscale");
		private static readonly int DebugShowAllChannels_ID = Shader.PropertyToID("_DebugShowAllChannels");


		private Material cloudMaterial;
		private Material cloudSkyMaterial;

		private Texture3D shapeTexture;
		private Texture3D detailTexture;
		private Texture2D weatherMap;
		private Texture2D blueNoise;


		private ContainerVis containerVis;

		public void Init(Material cloudMaterial, Material cloudSkyMaterial,
			Texture3D shapeTexture, Texture3D detailTexture, Texture2D weatherMap, Texture2D blueNoise)
		{
			this.cloudMaterial = cloudMaterial;
			this.cloudSkyMaterial = cloudSkyMaterial;
			this.shapeTexture = shapeTexture;
			this.detailTexture = detailTexture;
			this.weatherMap = weatherMap;
			this.blueNoise = blueNoise;
		}

		public void Setup()
		{
			if (containerVis == null)
			{
				containerVis = Object.FindObjectOfType<ContainerVis>();
			}
		}


		//TODO:可以添加GodRay效果  但是先要把云渲染到RT上 alpha 决定云的厚度
		//然后传入太阳位置  opaque下 有GodRay     最后blur  在合并到主贴图
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var settings = VolumeManager.instance.stack.GetComponent<CloudImageEffectPostProcess>();

			if (settings == null || !settings.IsActive())
			{
				return;
			}

			if (containerVis is null)
			{
				Debug.LogError(
					$"containerVis:{containerVis != null}");
				return;
			}

			var material = settings.useSkybox.value ? cloudSkyMaterial : cloudMaterial;

			var cmd = CommandBufferPool.Get(k_CloudImageEffectPass);
			var sampler = new ProfilingSampler(k_CloudImageEffectPass);

			using (new ProfilingScope(cmd, sampler))
			{
				material.SetTexture(NoiseTex_ID, shapeTexture);
				material.SetTexture(DetailNoiseTex_ID, detailTexture);
				material.SetTexture(BlueNoise_ID, blueNoise);
				material.SetTexture(WeatherMap_ID, weatherMap);


				material.SetFloat(Scale_ID, settings.cloudScale.value);
				material.SetFloat(DensityMultiplier_ID, settings.densityMultiplier.value);
				material.SetFloat(DensityOffset_ID, settings.densityOffset.value);
				material.SetFloat(LightAbsorptionThroughCloud_ID, settings.lightAbsorptionThroughCloud.value);
				material.SetFloat(LightAbsorptionTowardSun_ID, settings.lightAbsorptionTowardSun.value);
				material.SetFloat(DarknessThreshold_ID, settings.darknessThreshold.value);
				material.SetVector(Params_ID, settings.cloudTestParams.value);
				material.SetFloat(RayOffsetStrength_ID, settings.rayOffsetStrength.value);

				material.SetFloat(DetailNoiseScale_ID, settings.detailNoiseScale.value);
				material.SetFloat(DetailNoiseWeight_ID, settings.detailNoiseWeight.value);
				material.SetVector(ShapeOffset_ID, settings.shapeOffset.value);
				material.SetVector(DetailOffset_ID, settings.detailOffset.value);
				material.SetVector(DetailWeights_ID, settings.detailNoiseWeights.value);
				material.SetVector(ShapeNoiseWeights_ID, settings.shapeNoiseWeights.value);
				material.SetVector(PhaseParams_ID,
					new Vector4(settings.forwardScattering.value,
						settings.backScattering.value, settings.baseBrightness.value, settings.phaseFactor.value));

				var container = containerVis.transform;
				Vector3 pos = container.position;
				if (settings.followCamera.value)
				{
					var camPos = Camera.main.transform.position;
					pos += camPos;
				}
				Vector3 size = container.lossyScale;
				int width = Mathf.CeilToInt(size.x);
				int height = Mathf.CeilToInt(size.y);
				int depth = Mathf.CeilToInt(size.z);

				material.SetVector(BoundsMin_ID, pos - size / 2);
				material.SetVector(BoundsMax_ID, pos + size / 2);

				material.SetVector(MapSize_ID, new Vector4(width, height, depth, 0));

				material.SetInt(NumStepsLight_ID, settings.numStepsLight.value);

				material.SetFloat(TimeScale_ID, (Application.isPlaying) ? settings.timeScale.value : 0);
				material.SetFloat(BaseSpeed_ID, settings.baseSpeed.value);
				material.SetFloat(DetailSpeed_ID, settings.detailSpeed.value);

				SetDebugParams(settings, material);

				material.SetColor(ColA_ID, settings.colA.value);
				material.SetColor(ColB_ID, settings.colB.value);

				CoreUtils.DrawFullScreen(cmd, material);

				context.ExecuteCommandBuffer(cmd);
				context.Submit();
				CommandBufferPool.Release(cmd);
			}
		}

		void SetDebugParams(CloudImageEffectPostProcess settings, Material material)
		{
			material.SetInt(DebugViewMode_ID, (int) settings.debugMode.value);

			if (settings.debugMode.value != 0)
			{
				material.SetFloat(DebugNoiseSliceDepth_ID, settings.viewerSliceDepth.value);
				material.SetFloat(DebugTileAmount_ID, settings.viewerTileAmount.value);
				material.SetFloat(ViewerSize_ID, settings.viewerSize.value);

				Vector4 colorMask;
				switch (settings.viewerColorMask.value)
				{
					case CloudImageEffectPostProcess.ColorMask.R:
						colorMask = new Vector4(1, 0, 0, 0);
						break;
					case CloudImageEffectPostProcess.ColorMask.G:
						colorMask = new Vector4(0, 1, 0, 0);
						break;
					case CloudImageEffectPostProcess.ColorMask.B:
						colorMask = new Vector4(0, 0, 1, 0);
						break;
					case CloudImageEffectPostProcess.ColorMask.A:
						colorMask = new Vector4(0, 0, 0, 1);
						break;
					default:
						colorMask = Vector4.zero;
						break;
				}

				material.SetVector(DebugChannelWeight_ID, colorMask);
				material.SetInt(DebugGreyscale_ID, (settings.viewerGreyScale.value) ? 1 : 0);
				material.SetInt(DebugShowAllChannels_ID, (settings.viewerShadowAllChannels.value) ? 1 : 0);
			}
		}
	}
}