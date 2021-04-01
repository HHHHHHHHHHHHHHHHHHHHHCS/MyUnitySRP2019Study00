using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	public class SolidCloudRenderPass : ScriptableRenderPass
	{
		private const string k_SolidCloudPass = "SolidCloud";

		// private const string k_CLOUD_AREA_BOX = "CLOUD_AREA_BOX";
		private const string k_CLOUD_AREA_SPHERE = "CLOUD_AREA_SPHERE";
		private const string k_CLOUD_SUN_SHADOWS_ON = "CLOUD_SUN_SHADOWS_ON";
		private const string k_CLOUD_DISTANCE_ON = "CLOUD_DISTANCE_ON";
		private const string K_CLOUD_USE_XY_PLANE = "CLOUD_USE_XY_PLANE";
		
		private static readonly int CameraColorTexture_ID = Shader.PropertyToID("_CameraColorTexture");

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
		private static readonly int NoiseTex_ID = Shader.PropertyToID("_NoiseTex");
		private static readonly int CloudColor_ID = Shader.PropertyToID("_CloudColor");
		private static readonly int CloudStepping_ID = Shader.PropertyToID("_CloudStepping");
		private static readonly int CloudWindDir_ID = Shader.PropertyToID("_CloudWindDir");
		private static readonly int CloudData_ID = Shader.PropertyToID("_CloudData");
		private static readonly int CloudAreaPosition_ID = Shader.PropertyToID("_CloudAreaPosition");
		private static readonly int CloudAreaData_ID = Shader.PropertyToID("_CloudAreaData");
		private static readonly int CloudDistance_ID = Shader.PropertyToID("_CloudDistance");
		private static readonly int SunShadowsData_ID = Shader.PropertyToID("_SunShadowsData");

		private SolidCloudRenderPostProcess settings;
		private Material solidCloudMaterial;
		private Texture2D noiseTex;

		private Vector3 windSpeedAcum;
		private float amount;


		public void Init(Material solidCloudMaterial, Texture2D noiseTex)
		{
			this.solidCloudMaterial = solidCloudMaterial;
			this.noiseTex = noiseTex;
		}

		public void Setup(SolidCloudRenderPostProcess solidCloudRenderPostProcess)
		{
			settings = solidCloudRenderPostProcess;
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(k_SolidCloudPass);
			var sampler = new ProfilingSampler(k_SolidCloudPass);

			using (new ProfilingScope(cmd, sampler))
			{
				var mainLightIndex = renderingData.lightData.mainLightIndex;

				//gen noise 
				//###############################################
				//也可以用compute shader 代替
				//可以预烘焙 只生成一次
				solidCloudMaterial.SetTexture(NoiseTex_ID, noiseTex);
				int tw = 2 << settings.noisePowSize.value;
				RenderTexture genNoiseRT = RenderTexture.GetTemporary(tw, tw, 0,
					RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
				genNoiseRT.wrapMode = TextureWrapMode.Repeat;
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


				cmd.SetRenderTarget(genNoiseRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 1);

				context.ExecuteCommandBuffer(cmd);
				context.Submit();
				cmd.Clear();


				//random noise 
				//###############################################
				solidCloudMaterial.SetTexture(NoiseTex_ID, genNoiseRT);
				RenderTexture randomNoiseRT = RenderTexture.GetTemporary(tw, tw, 0,
					RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
				randomNoiseRT.wrapMode = TextureWrapMode.Repeat;
				amount += Time.deltaTime * settings.noiseSpeed.value;
				solidCloudMaterial.SetFloat(Amount_ID, amount);
				cmd.SetRenderTarget(randomNoiseRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 2);

				context.ExecuteCommandBuffer(cmd);
				context.Submit();
				cmd.Clear();


				//raymarch cloud
				//###############################################
				solidCloudMaterial.SetTexture(NoiseTex_ID, randomNoiseRT);


				//CloudColor----------------
				solidCloudMaterial.SetVector(CloudColor_ID, settings.cloudAlbedoColor.value);

				CoreUtils.SetKeyword(solidCloudMaterial, K_CLOUD_USE_XY_PLANE, settings.useXYPlane.value);


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


				// cmd.GetTemporaryRT(tid,1920,1080,0, FilterMode.Point,GraphicsFormat.R8G8B8A8_UNorm);
				cmd.SetRenderTarget(CameraColorTexture_ID);
				// cmd.ClearRenderTarget(true, true, Color.black);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
				// cmd.ReleaseTemporaryRT(tid);


				RenderTexture.ReleaseTemporary(genNoiseRT);
				RenderTexture.ReleaseTemporary(randomNoiseRT);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
			context.Submit();
		}
	}
}