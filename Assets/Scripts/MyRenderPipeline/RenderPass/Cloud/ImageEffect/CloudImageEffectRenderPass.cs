using MyRenderPipeline.RenderPass.GodRay;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class CloudImageEffectRenderPass : ScriptableRenderPass
	{
		private const string k_CloudImageEffectPass = "CloudImageEffectPass";

		private const string c_OnlyCloud = "_OnlyCloud";

		private static readonly int cloudRT_ID = Shader.PropertyToID("_CloudRT");
		private static readonly RenderTargetIdentifier cloudRT_RTI = new RenderTargetIdentifier(cloudRT_ID);

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

		private CloudImageEffectPostProcess cloudSettings;

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

		public void Setup(CloudImageEffectPostProcess cloudSettings)
		{
			if (containerVis == null)
			{
				containerVis = Object.FindObjectOfType<ContainerVis>();
			}

			this.cloudSettings = cloudSettings;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			var desc = cameraTextureDescriptor;
			desc.depthBufferBits = 0;
			cmd.GetTemporaryRT(cloudRT_ID, desc);
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			cmd.ReleaseTemporaryRT(cloudRT_ID);
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (containerVis is null)
			{
				Debug.LogError(
					$"containerVis:{containerVis != null}");
				return;
			}


			bool enableGodRay = false;

			var godRaySettings = VolumeManager.instance.stack.GetComponent<GodRayPostProcess>();

			if (godRaySettings && godRaySettings.IsActive())
			{
				enableGodRay = true;
			}

			//开启godray 现在只支持 onlyCloudMaterial
			Material material = enableGodRay || !cloudSettings.useSkybox.value ? cloudMaterial : cloudSkyMaterial;

			var cmd = CommandBufferPool.Get(k_CloudImageEffectPass);
			var sampler = new ProfilingSampler(k_CloudImageEffectPass);

			using (new ProfilingScope(cmd, sampler))
			{
				CoreUtils.SetKeyword(material, c_OnlyCloud, enableGodRay);

				material.SetTexture(NoiseTex_ID, shapeTexture);
				material.SetTexture(DetailNoiseTex_ID, detailTexture);
				material.SetTexture(BlueNoise_ID, blueNoise);
				material.SetTexture(WeatherMap_ID, weatherMap);

				material.SetFloat(Scale_ID, cloudSettings.cloudScale.value);
				material.SetFloat(DensityMultiplier_ID, cloudSettings.densityMultiplier.value);
				material.SetFloat(DensityOffset_ID, cloudSettings.densityOffset.value);
				material.SetFloat(LightAbsorptionThroughCloud_ID, cloudSettings.lightAbsorptionThroughCloud.value);
				material.SetFloat(LightAbsorptionTowardSun_ID, cloudSettings.lightAbsorptionTowardSun.value);
				material.SetFloat(DarknessThreshold_ID, cloudSettings.darknessThreshold.value);
				material.SetVector(Params_ID, cloudSettings.cloudTestParams.value);
				material.SetFloat(RayOffsetStrength_ID, cloudSettings.rayOffsetStrength.value);

				material.SetFloat(DetailNoiseScale_ID, cloudSettings.detailNoiseScale.value);
				material.SetFloat(DetailNoiseWeight_ID, cloudSettings.detailNoiseWeight.value);
				material.SetVector(ShapeOffset_ID, cloudSettings.shapeOffset.value);
				material.SetVector(DetailOffset_ID, cloudSettings.detailOffset.value);
				material.SetVector(DetailWeights_ID, cloudSettings.detailNoiseWeights.value);
				material.SetVector(ShapeNoiseWeights_ID, cloudSettings.shapeNoiseWeights.value);
				material.SetVector(PhaseParams_ID,
					new Vector4(cloudSettings.forwardScattering.value,
						cloudSettings.backScattering.value, cloudSettings.baseBrightness.value,
						cloudSettings.phaseFactor.value));

				var container = containerVis.transform;
				Vector3 pos = container.position;
				if (cloudSettings.followCamera.value)
				{
					var camPos = Camera.main.transform.position; //renderingData.cameraData.camera.transform.position;
					pos += camPos;
				}

				Vector3 size = container.lossyScale;
				int width = Mathf.CeilToInt(size.x);
				int height = Mathf.CeilToInt(size.y);
				int depth = Mathf.CeilToInt(size.z);

				material.SetVector(BoundsMin_ID, pos - size / 2);
				material.SetVector(BoundsMax_ID, pos + size / 2);

				material.SetVector(MapSize_ID, new Vector4(width, height, depth, 0));

				material.SetInt(NumStepsLight_ID, cloudSettings.numStepsLight.value);

				material.SetFloat(TimeScale_ID, (Application.isPlaying) ? cloudSettings.timeScale.value : 0);
				material.SetFloat(BaseSpeed_ID, cloudSettings.baseSpeed.value);
				material.SetFloat(DetailSpeed_ID, cloudSettings.detailSpeed.value);

				SetDebugParams(cloudSettings, material);

				material.SetColor(ColA_ID, cloudSettings.colA.value);
				material.SetColor(ColB_ID, cloudSettings.colB.value);

				if (enableGodRay)
				{
					//var msaa = renderingData.cameraData.cameraTargetDescriptor.msaaSamples > 1;
					cmd.SetRenderTarget(cloudRT_RTI,
						RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
					// cmd.ClearRenderTarget(false, true, Color.black);
					// cmd.SetRenderTarget(cloudRT_RTI);


					cmd.SetGlobalTexture(cloudRT_ID, cloudRT_RTI);
				}

				CoreUtils.DrawFullScreen(cmd, material);

			}
			
			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
			context.Submit();
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