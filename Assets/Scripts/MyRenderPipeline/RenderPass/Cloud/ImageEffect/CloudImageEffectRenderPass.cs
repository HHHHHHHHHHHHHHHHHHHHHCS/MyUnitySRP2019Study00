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
		private static readonly int LightAbsorptionThroughCloud_ID = Shader.PropertyToID("_LightAbsorptionThroughCloud");
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

		
		private Material material;
		private Texture2D blueNoise;

		private WeatherMap weatherMap;
		private NoiseGenerator noiseGenerator;
		private ContainerVis containerVis;
		
		public void Init(Material material, Texture2D blueNoise)
		{
			this.material = material;
			this.blueNoise = blueNoise;
		}

		public void Setup()
		{
			var settings = VolumeManager.instance.stack.GetComponent<CloudImageEffectPostProcess>();

			if (weatherMap == null)
			{
				weatherMap = Object.FindObjectOfType<WeatherMap>();
				if (weatherMap != null)
				{
					if (settings != null)
					{
						weatherMap.UpdateMap(settings.heightOffset.value);
					}
					else
					{
						weatherMap.UpdateMap(Vector2.zero);
					}
				}
			}

			if (noiseGenerator == null)
			{
				noiseGenerator = Object.FindObjectOfType<NoiseGenerator>();
				if (noiseGenerator != null)
				{
					noiseGenerator.UpdateNoise();
				}
			}

			if (containerVis == null)
			{
				containerVis = Object.FindObjectOfType<ContainerVis>();
			}
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var settings = VolumeManager.instance.stack.GetComponent<CloudImageEffectPostProcess>();

			if (settings == null || !settings.IsActive())
			{
				return;
			}

			if (weatherMap is null || noiseGenerator is null || containerVis is null)
			{
				Debug.LogError(
					$"weatherMap: {weatherMap != null}, noiseGenerator: {noiseGenerator != null},containerVis:{containerVis != null}");
				return;
			}

			//TODO:生成全部的Noise  然后raymarch 再看看

			var cmd = CommandBufferPool.Get(k_CloudImageEffectPass);
			var sampler = new ProfilingSampler(k_CloudImageEffectPass);
			
			using (new ProfilingScope(cmd, sampler))
			{
				material.SetTexture(NoiseTex_ID, noiseGenerator.shapeTexture);
				material.SetTexture(DetailNoiseTex_ID, noiseGenerator.detailTexture);
				material.SetTexture(BlueNoise_ID, blueNoise);
				material.SetTexture(WeatherMap_ID, weatherMap.weatherMap);


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

				SetDebugParams();

				material.SetColor(ColA_ID, settings.colA.value);
				material.SetColor(ColB_ID, settings.colB.value);


				CoreUtils.DrawFullScreen(cmd, material);
				
				context.ExecuteCommandBuffer(cmd);
				context.Submit();
				CommandBufferPool.Release(cmd);
			}

		}

		void SetDebugParams()
		{
			int debugModeIndex = 0;
			if (noiseGenerator.viewerEnabled)
			{
				debugModeIndex = (noiseGenerator.activeTextureType == NoiseGenerator.CloudNoiseType.Shape) ? 1 : 2;
			}

			if (weatherMap.viewerEnabled)
			{
				debugModeIndex = 3;
			}

			material.SetInt(DebugViewMode_ID, debugModeIndex);
			material.SetFloat(DebugNoiseSliceDepth_ID, noiseGenerator.viewerSliceDepth);
			material.SetFloat(DebugTileAmount_ID, noiseGenerator.viewerTileAmount);
			material.SetFloat(ViewerSize_ID, noiseGenerator.viewerSize);
			material.SetVector(DebugChannelWeight_ID, noiseGenerator.ChannelMask);
			material.SetInt(DebugGreyscale_ID, (noiseGenerator.viewerGreyscale) ? 1 : 0);
			material.SetInt(DebugShowAllChannels_ID, (noiseGenerator.viewerShowAllChannels) ? 1 : 0);
		}
	}
}