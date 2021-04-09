using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	public class SolidCloudRenderPass : ScriptableRenderPass
	{
		private const string k_SolidCloudPass = "SolidCloud";

		private const string k_CLOUD_MASK = "CLOUD_MASK";

		private const string k_CLOUD_USE_XY_PLANE = "CLOUD_USE_XY_PLANE";

		// private const string k_CLOUD_AREA_BOX = "CLOUD_AREA_BOX";
		private const string k_CLOUD_AREA_SPHERE = "CLOUD_AREA_SPHERE";
		private const string k_CLOUD_SUN_SHADOWS_ON = "CLOUD_SUN_SHADOWS_ON";
		private const string k_CLOUD_DISTANCE_ON = "CLOUD_DISTANCE_ON";
		private const string k_CLOUD_BLUR_ON = "CLOUD_BLUR_ON";

		private static readonly int GenNoiseTex_ID = Shader.PropertyToID("_GenNoiseTex");
		private static readonly int RandomNoiseTex_ID = Shader.PropertyToID("_RandomNoiseTex");
		private static readonly int BlendTex_ID = Shader.PropertyToID("_BlendTex");


		private static readonly RenderTargetIdentifier CameraColorTexture_RTI =
			new RenderTargetIdentifier("_CameraColorTexture");

		private static readonly RenderTargetIdentifier GenNoiseTex_RTI =
			new RenderTargetIdentifier(GenNoiseTex_ID);

		private static readonly RenderTargetIdentifier RandomNoiseTex_RTI =
			new RenderTargetIdentifier(RandomNoiseTex_ID);

		private static readonly RenderTargetIdentifier BlendTex_RTI =
			new RenderTargetIdentifier(BlendTex_ID);

		//generate noise##################
		private static readonly int NoiseStrength_ID = Shader.PropertyToID("_NoiseStrength");
		private static readonly int NoiseDensity_ID = Shader.PropertyToID("_NoiseDensity");
		private static readonly int LightColor_ID = Shader.PropertyToID("_LightColor");
		private static readonly int SpecularColor_ID = Shader.PropertyToID("_SpecularColor");
		private static readonly int NoiseSize_ID = Shader.PropertyToID("_NoiseSize");
		private static readonly int NoiseCount_ID = Shader.PropertyToID("_NoiseCount");
		private static readonly int NoiseSeed_ID = Shader.PropertyToID("_NoiseSeed");

		//random noise##################
		private static readonly int Amount_ID = Shader.PropertyToID("_Amount");

		//cloud##################
		private static readonly int DstBlend_ID = Shader.PropertyToID("_DstBlend");
		private static readonly int NoiseTex_ID = Shader.PropertyToID("_NoiseTex");
		private static readonly int MaskTex_ID = Shader.PropertyToID("_MaskTex");
		private static readonly int CloudColor_ID = Shader.PropertyToID("_CloudColor");
		private static readonly int CloudStepping_ID = Shader.PropertyToID("_CloudStepping");
		private static readonly int CloudWindDir_ID = Shader.PropertyToID("_CloudWindDir");
		private static readonly int CloudData_ID = Shader.PropertyToID("_CloudData");
		private static readonly int CloudAreaPosition_ID = Shader.PropertyToID("_CloudAreaPosition");
		private static readonly int CloudAreaData_ID = Shader.PropertyToID("_CloudAreaData");
		private static readonly int CloudDistance_ID = Shader.PropertyToID("_CloudDistance");
		private static readonly int SunShadowsData_ID = Shader.PropertyToID("_SunShadowsData");

		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_SolidCloudPass);

		private SolidCloudRenderPostProcess settings;
		private Material solidCloudMaterial;
		private Texture2D noiseTex;
		private Texture2D maskTex;


		private Vector3 windSpeedAcum;
		private float amount;

		private RenderTexture genNoiseRT, randomNoiseRT, blendRT;


		public void Init(Material solidCloudMaterial, Texture2D noiseTex)
		{
			this.solidCloudMaterial = solidCloudMaterial;
			this.noiseTex = noiseTex;
		}

		public void Setup(SolidCloudRenderPostProcess solidCloudRenderPostProcess)
		{
			settings = solidCloudRenderPostProcess;
		}

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			// int tw = 2 << settings.noisePowSize.value;
			//
			// genNoiseRT = RenderTexture.GetTemporary(tw, tw, 0,
			// 	RenderTextureFormat.ARGB32);
			// genNoiseRT.wrapMode = TextureWrapMode.Repeat;
			//
			// randomNoiseRT = RenderTexture.GetTemporary(tw, tw, 0,
			// 	RenderTextureFormat.ARGB32);
			// randomNoiseRT.wrapMode = TextureWrapMode.Repeat;
			//
			// int rtSize = settings.rtSize.value;
			//
			// if (rtSize != 1)
			// {
			// 	int width = cameraTextureDescriptor.width / rtSize;
			// 	int height = cameraTextureDescriptor.height / rtSize;
			// 	blendRT = RenderTexture.GetTemporary(width, height, 0
			// 		, RenderTextureFormat.ARGB32);
			// }
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			if (genNoiseRT != null)
			{
				RenderTexture.ReleaseTemporary(genNoiseRT);
				genNoiseRT = null;
			}

			if (randomNoiseRT != null)
			{
				RenderTexture.ReleaseTemporary(randomNoiseRT);
				randomNoiseRT = null;
			}

			if (blendRT != null)
			{
				RenderTexture.ReleaseTemporary(blendRT);
				blendRT = null;
			}
		}


		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(k_SolidCloudPass);

			using (new ProfilingScope(cmd, profilingSampler))
			{
				var mainLightIndex = renderingData.lightData.mainLightIndex;

				//gen noise 
				//###############################################
				//也可以用compute shader 代替
				//可以预烘焙 只生成一次
				cmd.SetGlobalTexture(NoiseTex_ID, noiseTex);
				int tw = 2 << settings.noisePowSize.value;

				solidCloudMaterial.SetFloat(NoiseStrength_ID, settings.noiseStrength.value);
				solidCloudMaterial.SetFloat(NoiseDensity_ID, settings.noiseDensity.value);


				Vector3 mainLightDir;
				Color mainLightColor;
				if (mainLightIndex >= 0)
				{
					var lightData = renderingData.lightData.visibleLights[mainLightIndex];
					mainLightDir = lightData.light.transform.forward;
					mainLightColor = lightData.finalColor;
				}
				else
				{
					mainLightDir = Vector3.down;
					mainLightColor = Color.white;
				}

				Vector3 nlight = new Vector3(-mainLightDir.x, 0, -mainLightDir.z).normalized * 0.3f;
				nlight.y = mainLightDir.y > 0
					? Mathf.Clamp01(1.0f - mainLightDir.y)
					: 1.0f - Mathf.Clamp01(-mainLightDir.y);
				Color specularColor = settings.cloudSpecularColor.value;
				specularColor.r *= 0.5f;
				specularColor.g *= 0.5f;
				specularColor.b *= 0.5f;
				specularColor.a = nlight.y / (1.0001f - specularColor.a);


				solidCloudMaterial.SetColor(LightColor_ID, mainLightColor * 0.5f);
				solidCloudMaterial.SetColor(SpecularColor_ID, specularColor);

				int nz = Mathf.FloorToInt(nlight.z * tw) * tw;
				int noiseSeed = (int) (nz + nlight.x * tw) + tw * tw;

				solidCloudMaterial.SetInt(NoiseSize_ID, tw);
				solidCloudMaterial.SetInt(NoiseCount_ID, tw * tw);
				solidCloudMaterial.SetInt(NoiseSeed_ID, noiseSeed);

				cmd.GetTemporaryRT(GenNoiseTex_ID, tw, tw, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
				cmd.SetRenderTarget(GenNoiseTex_RTI, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 1);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();


				//random noise 
				//###############################################
				cmd.SetGlobalTexture(NoiseTex_ID, GenNoiseTex_RTI);

				amount += Time.deltaTime * settings.noiseSpeed.value;
				solidCloudMaterial.SetFloat(Amount_ID, amount);
				cmd.GetTemporaryRT(RandomNoiseTex_ID, tw, tw, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
				cmd.SetRenderTarget(RandomNoiseTex_RTI, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 2);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();


				//raymarch cloud
				//###############################################
				cmd.SetGlobalTexture(NoiseTex_ID, RandomNoiseTex_RTI);
				if (settings.enableMask.value && settings.maskTexture.value != null)
				{
					CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_MASK, true);
					solidCloudMaterial.SetTexture(MaskTex_ID, settings.maskTexture.value);
				}
				else
				{
					CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_MASK, false);
					solidCloudMaterial.SetTexture(MaskTex_ID, null);
				}


				//CloudColor----------------
				solidCloudMaterial.SetVector(CloudColor_ID, settings.cloudAlbedoColor.value);

				CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_USE_XY_PLANE, settings.useXYPlane.value);


				float scale = 0.01f / settings.noiseScale.value;

				//CloudDistance_ID----------------
				float distance = settings.distance.value;
				float distanceFallOff = settings.distanceFallOff.value;
				float maxLength = settings.maxLength.value;
				float maxLengthFallOff = settings.maxLengthFallOff.value * maxLength + 1.0f;

				CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_DISTANCE_ON, distance > 0);

				solidCloudMaterial.SetVector(CloudDistance_ID,
					new Vector4(scale * scale * distance * distance, (distanceFallOff * distanceFallOff + 0.1f),
						maxLength, maxLengthFallOff)); //, _distance * (1.0f - _distanceFallOff)));


				//CloudWindDir_ID-----------------
				windSpeedAcum += Time.deltaTime * settings.windDirection.value.normalized
				                                * settings.windSpeed.value / scale;
				if (windSpeedAcum.sqrMagnitude > 1000000) //1000*1000
				{
					windSpeedAcum = windSpeedAcum.normalized * windSpeedAcum.magnitude / 1000;
				}

				solidCloudMaterial.SetVector(CloudWindDir_ID, windSpeedAcum);


				//CloudData_ID---------------------
				Vector3 cloudAreaPosition = settings.cloudAreaPosition.value;

				solidCloudMaterial.SetVector(CloudData_ID
					, new Vector4(0, settings.height.value, 1.0f / settings.density.value, scale));

				//CloudAreaPosition_ID---------------
				// solidCloudMaterial.SetVector(CloudAreaPosition_ID,
				// 	new Vector4(cloudAreaPosition.x, 0, cloudAreaPosition.z, 0));

				solidCloudMaterial.SetVector(CloudAreaPosition_ID, cloudAreaPosition);

				//CloudAreaData_ID-----------
				float cloudAreaRadius = settings.cloudAreaRadius.value;
				float cloudAreaHeight = settings.cloudAreaHeight.value;
				float cloudAreaDepth = settings.cloudAreaDepth.value;
				float cloudAreaFallOff = settings.cloudAreaFallOff.value;

				Vector4 areaData = new Vector4(1.0f / (1.0f + cloudAreaRadius), 1.0f / (1.0f + cloudAreaHeight),
					1.0f / (1.0f + cloudAreaDepth), cloudAreaFallOff);
				if (cloudAreaHeight > 0 && cloudAreaDepth > 0)
				{
					solidCloudMaterial.DisableKeyword(k_CLOUD_AREA_SPHERE);
				}
				else
				{
					solidCloudMaterial.EnableKeyword(k_CLOUD_AREA_SPHERE);

					areaData.y = cloudAreaRadius * cloudAreaRadius;
					areaData.x /= scale;
					areaData.z /= scale;
				}

				solidCloudMaterial.SetVector(CloudAreaData_ID, areaData);


				//SunShadowsData_ID------------				
				float shadowStrength = settings.sunShadowsStrength.value;
				if (shadowStrength <= 0)
				{
					solidCloudMaterial.DisableKeyword(k_CLOUD_SUN_SHADOWS_ON);
				}
				else
				{
					if (mainLightIndex >= 0)
					{
						solidCloudMaterial.EnableKeyword(k_CLOUD_SUN_SHADOWS_ON);
						solidCloudMaterial.SetVector(SunShadowsData_ID,
							new Vector4(shadowStrength
								, settings.sunShadowsJitterStrength.value, settings.sunShadowsCancellation.value, 0));
					}
					else
					{
						solidCloudMaterial.DisableKeyword(k_CLOUD_SUN_SHADOWS_ON);
					}
				}


				//CloudStepping_ID------------
				float stepping = settings.stepping.value;
				float steppingNear = settings.steppingNear.value;
				float dithering = settings.ditherStrength.value;

				Vector4 fogStepping = new Vector4(1.0f / (stepping + 1.0f), 1 / (1 + steppingNear), 0,
					dithering * 0.1f);
				solidCloudMaterial.SetVector(CloudStepping_ID, fogStepping);


				//cmd get 有时候不靠谱

				int rtSize = settings.rtSize.value;
				if (rtSize == 1)
				{
					cmd.SetGlobalInt(DstBlend_ID,
						(int) (settings.enableBlend.value ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
					cmd.SetRenderTarget(CameraColorTexture_RTI);
					CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
				}
				else
				{
					cmd.SetGlobalInt(DstBlend_ID, (int) BlendMode.Zero);

					//blend
					int width = renderingData.cameraData.camera.scaledPixelWidth / rtSize;
					int height = renderingData.cameraData.camera.scaledPixelHeight / rtSize;

					cmd.GetTemporaryRT(BlendTex_ID, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
					cmd.SetRenderTarget(BlendTex_RTI, RenderBufferLoadAction.DontCare,
						RenderBufferStoreAction.Store,
						RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
					CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);

					context.ExecuteCommandBuffer(cmd);
					cmd.Clear();

					cmd.SetGlobalTexture(NoiseTex_ID, BlendTex_RTI);
					
					CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_BLUR_ON, settings.enableBlur.value);
					cmd.SetGlobalInt(DstBlend_ID,
						(int) (settings.enableBlend.value ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
					cmd.SetRenderTarget(CameraColorTexture_RTI);
					CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 3);
				}

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				cmd.ReleaseTemporaryRT(GenNoiseTex_ID);
				cmd.ReleaseTemporaryRT(RandomNoiseTex_ID);
				if (rtSize != 1)
				{
					cmd.ReleaseTemporaryRT(BlendTex_ID);
				}
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}