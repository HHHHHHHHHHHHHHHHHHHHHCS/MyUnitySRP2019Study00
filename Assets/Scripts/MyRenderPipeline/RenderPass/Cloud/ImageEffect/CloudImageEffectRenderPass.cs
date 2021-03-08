using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class CloudImageEffectRenderPass : ScriptableRenderPass
	{
		private const string k_CloudImageEffectPass = "CloudImageEffectPass";

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
				if (settings != null)
				{
					weatherMap.UpdateMap(settings.heightOffset.value);
				}
				else
				{
					weatherMap.UpdateMap(Vector2.zero);
				}
			}

			if (noiseGenerator == null)
			{
				noiseGenerator = Object.FindObjectOfType<NoiseGenerator>();
				noiseGenerator.UpdateNoise();
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


			var cmd = CommandBufferPool.Get(k_CloudImageEffectPass);
			var sampler = new ProfilingSampler(k_CloudImageEffectPass);
			
			using (new ProfilingScope(cmd, sampler))
			{
				material.SetTexture("NoiseTex", noiseGenerator.shapeTexture);
				material.SetTexture("DetailNoiseTex", noiseGenerator.detailTexture);
				material.SetTexture("BlueNoise", blueNoise);
				material.SetTexture("WeatherMap", weatherMap.weatherMap);

				var container = containerVis.transform;
				Vector3 size = containerVis.transform.lossyScale;
				int width = Mathf.CeilToInt(size.x);
				int height = Mathf.CeilToInt(size.y);
				int depth = Mathf.CeilToInt(size.z);

				material.SetFloat("scale", settings.cloudScale.value);
				material.SetFloat("densityMultiplier", settings.densityMultiplier.value);
				material.SetFloat("densityOffset", settings.densityOffset.value);
				material.SetFloat("lightAbsorptionThroughCloud", settings.lightAbsorptionThroughCloud.value);
				material.SetFloat("lightAbsorptionTowardSun", settings.lightAbsorptionTowardSun.value);
				material.SetFloat("darknessThreshold", settings.darknessThreshold.value);
				material.SetVector("params", settings.cloudTestParams.value);
				material.SetFloat("rayOffsetStrength", settings.rayOffsetStrength.value);

				material.SetFloat("detailNoiseScale", settings.detailNoiseScale.value);
				material.SetFloat("detailNoiseWeight", settings.detailNoiseWeight.value);
				material.SetVector("shapeOffset", settings.shapeOffset.value);
				material.SetVector("detailOffset", settings.detailOffset.value);
				material.SetVector("detailWeights", settings.detailNoiseWeights.value);
				material.SetVector("shapeNoiseWeights", settings.shapeNoiseWeights.value);
				material.SetVector("phaseParams",
					new Vector4(settings.forwardScattering.value,
						settings.backScattering.value, settings.baseBrightness.value, settings.phaseFactor.value));

				material.SetVector("boundsMin", container.position - container.localScale / 2);
				material.SetVector("boundsMax", container.position + container.localScale / 2);

				material.SetInt("numStepsLight", settings.numStepsLight.value);

				material.SetVector("mapSize", new Vector4(width, height, depth, 0));

				material.SetFloat("timeScale", (Application.isPlaying) ? settings.timeScale.value : 0);
				material.SetFloat("baseSpeed", settings.baseSpeed.value);
				material.SetFloat("detailSpeed", settings.detailSpeed.value);

				SetDebugParams();

				material.SetColor("colA", settings.colA.value);
				material.SetColor("colB", settings.colB.value);



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

			material.SetInt("debugViewMode", debugModeIndex);
			material.SetFloat("debugNoiseSliceDepth", noiseGenerator.viewerSliceDepth);
			material.SetFloat("debugTileAmount", noiseGenerator.viewerTileAmount);
			material.SetFloat("viewerSize", noiseGenerator.viewerSize);
			material.SetVector("debugChannelWeight", noiseGenerator.ChannelMask);
			material.SetInt("debugGreyscale", (noiseGenerator.viewerGreyscale) ? 1 : 0);
			material.SetInt("debugShowAllChannels", (noiseGenerator.viewerShowAllChannels) ? 1 : 0);
		}
	}
}