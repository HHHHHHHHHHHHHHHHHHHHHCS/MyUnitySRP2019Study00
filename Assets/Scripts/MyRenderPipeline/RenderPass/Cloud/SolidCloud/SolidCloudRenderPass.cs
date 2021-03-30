using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	public class SolidCloudRenderPass : ScriptableRenderPass
	{
		private const string k_SolidCloudPass = "SolidCloud";

		private const string k_CLOUD_AREA_BOX = "CLOUD_AREA_BOX";
		private const string k_CLOUD_AREA_SPHERE = "CLOUD_AREA_SPHERE";
		private const string k_CLOUD_SUN_SHADOWS_ON = "CLOUD_SUN_SHADOWS_ON";
		private const string k_CLOUD_DISTANCE_ONs = "CLOUD_DISTANCE_ON";


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
		private static readonly int Amount = Shader.PropertyToID("_Amount");


		public void Init(Material solidCloudMaterial, Texture2D noiseTex)
		{
			this.solidCloudMaterial = solidCloudMaterial;
			this.noiseTex = noiseTex;
		}

		public void Setup(SolidCloudRenderPostProcess solidCloudRenderPostProcess)
		{
			settings = solidCloudRenderPostProcess;
		}

		/*
		void UpdateTextureColors(Color[] colors, bool forceUpdateEntireTexture)
		{
			Vector3 nlight;
			int nz, disp;
			float nyspec;
			float spec = 1.0001f - _specularThreshold;
			int tw = adjustedTexture.width;
			nlight = new Vector3(-_lightDirection.x, 0, -_lightDirection.z).normalized * 0.3f;
			nlight.y = _lightDirection.y > 0
				? Mathf.Clamp01(1.0f - _lightDirection.y)
				: 1.0f - Mathf.Clamp01(-_lightDirection.y);
			nz = Mathf.FloorToInt(nlight.z * tw) * tw;
			disp = (int) (nz + nlight.x * tw) + colors.Length;
			nyspec = nlight.y / spec;
			Color specularColor = currentFogSpecularColor * (1.0f + _specularIntensity) * _specularIntensity;
			bool hasChanged = false;
			if (updatingTextureSlice >= 1 || forceUpdateEntireTexture)
				hasChanged = true;
			float lcr = updatingTextureLightColor.r * 0.5f;
			float lcg = updatingTextureLightColor.g * 0.5f;
			float lcb = updatingTextureLightColor.b * 0.5f;
			float scr = specularColor.r * 0.5f;
			float scg = specularColor.g * 0.5f;
			float scb = specularColor.b * 0.5f;

			int count = colors.Length;
			int k0 = 0;
			int k1 = count;
			if (updatingTextureSlice >= 0)
			{
				if (updatingTextureSlice > _updateTextureSpread)
				{
					// detected change of configuration amid texture updates
					updatingTextureSlice = -1;
					needUpdateTexture = true;
					return;
				}

				k0 = count * updatingTextureSlice / _updateTextureSpread;
				k1 = count * (updatingTextureSlice + 1) / _updateTextureSpread;
			}

			int z = 0;
			for (int k = k0; k < k1; k++)
			{
				int indexg = (k + disp) % count;
				float a = colors[k].a;
				float r = (a - colors[indexg].a) * nyspec;
				if (r < 0f)
					r = 0f;
				else if (r > 1f)
					r = 1f;
				float cor = lcr + scr * r;
				float cog = lcg + scg * r;
				float cob = lcb + scb * r;
				if (!hasChanged)
				{
					if (z++ < 100)
					{
						if (cor != colors[k].r || cog != colors[k].g || cob != colors[k].b)
						{
							hasChanged = true;
						}
					}
					else if (!hasChanged)
					{
						break;
					}
				}

				colors[k].r = cor;
				colors[k].g = cog;
				colors[k].b = cob;
			}

			bool hasNewTextureData = forceUpdateEntireTexture;
			if (hasChanged)
			{
				if (updatingTextureSlice >= 0)
				{
					updatingTextureSlice++;
					if (updatingTextureSlice >= _updateTextureSpread)
					{
						updatingTextureSlice = -1;
						hasNewTextureData = true;
					}
				}
				else
				{
					hasNewTextureData = true;
				}
			}
			else
			{
				updatingTextureSlice = -1;
			}

			if (hasNewTextureData)
			{
				if (Application.isPlaying && _turbulenceStrength > 0f && adjustedChaosTexture)
				{
					adjustedChaosTexture.SetPixels(adjustedColors);
					adjustedChaosTexture.Apply();
				}
				else
				{
					adjustedTexture.SetPixels(adjustedColors);
					adjustedTexture.Apply();
					fogMat.SetTexture("_NoiseTex", adjustedTexture);
				}

				lastTextureUpdate = Time.time;
			}
		}

		void ApplyChaos()
		{
			if (!adjustedTexture)
				return;

			if (chaosLerpMat == null)
			{
				Shader chaosLerp = Shader.Find("VolumetricFogAndMist/Chaos Lerp");
				chaosLerpMat = new Material(chaosLerp);
				chaosLerpMat.hideFlags = HideFlags.DontSave;
			}

			turbAcum += Time.deltaTime * _turbulenceStrength;
			chaosLerpMat.SetFloat("_Amount", turbAcum);

			if (!adjustedChaosTexture)
			{
				adjustedChaosTexture = Instantiate(adjustedTexture) as Texture2D;
				adjustedChaosTexture.hideFlags = HideFlags.DontSave;
			}

			RenderTexture rtAdjusted = RenderTexture.GetTemporary(adjustedTexture.width, adjustedTexture.height, 0,
				RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			rtAdjusted.wrapMode = TextureWrapMode.Repeat;
			Graphics.Blit(adjustedChaosTexture, rtAdjusted, chaosLerpMat);
			fogMat.SetTexture("_NoiseTex", rtAdjusted);
			RenderTexture.ReleaseTemporary(rtAdjusted);
		}
		*/

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(k_SolidCloudPass);
			var sampler = new ProfilingSampler(k_SolidCloudPass);

			using (new ProfilingScope(cmd, sampler))
			{
				solidCloudMaterial.SetTexture(NoiseTex_ID, noiseTex);
				RenderTexture tempNoiseRT = RenderTexture.GetTemporary(noiseTex.width, noiseTex.height, 0,
					RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
				tempNoiseRT.wrapMode = TextureWrapMode.Repeat;
				amount += Time.deltaTime * settings.noiseSpeed.value;
				solidCloudMaterial.SetFloat(Amount, amount);
				cmd.SetRenderTarget(tempNoiseRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 1);

				context.ExecuteCommandBuffer(cmd);
				context.Submit();
				cmd.Clear();

				solidCloudMaterial.SetTexture(NoiseTex_ID, tempNoiseRT);

				//CloudColor----------------
				solidCloudMaterial.SetVector(CloudColor_ID, settings.cloudAlbedoColor.value);


				float scale = 0.01f / settings.noiseScale.value;

				//CloudDistance_ID----------------
				float distance = settings.distance.value;
				float distanceFallOff = settings.distanceFallOff.value;
				float maxLength = settings.maxLength.value;
				float maxLengthFallOff = settings.maxLengthFallOff.value;

				CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_DISTANCE_ONs, distance > 0);

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
					, new Vector4(cloudAreaPosition.y, settings.height.value, 1.0f / settings.density.value, scale));

				//CloudAreaPosition_ID---------------
				solidCloudMaterial.SetVector(CloudAreaPosition_ID,
					new Vector4(cloudAreaPosition.x, 0, cloudAreaPosition.z, 0));

				//CloudAreaData_ID-----------
				float cloudAreaRadius = settings.cloudAreaRadius.value;
				float cloudAreaHeight = settings.cloudAreaHeight.value;
				float cloudAreaDepth = settings.cloudAreaDepth.value;
				float cloudAreaFallOff = settings.cloudAreaFallOff.value;

				Vector4 areaData = new Vector4(1.0f / (1.0f + cloudAreaRadius), 1.0f / (1.0f + cloudAreaHeight),
					1.0f / (1.0f + cloudAreaDepth), cloudAreaFallOff);
				if (cloudAreaHeight > 0 && cloudAreaDepth > 0)
				{
					solidCloudMaterial.EnableKeyword(k_CLOUD_AREA_BOX);
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
					var mainLightIndex = renderingData.lightData.mainLightIndex;
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


				int tid = Shader.PropertyToID("_CameraColorTexture");
				// cmd.GetTemporaryRT(tid,1920,1080,0, FilterMode.Point,GraphicsFormat.R8G8B8A8_UNorm);
				cmd.SetRenderTarget(tid);
				cmd.ClearRenderTarget(true, true, Color.black);
				CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
				// cmd.ReleaseTemporaryRT(tid);


				RenderTexture.ReleaseTemporary(tempNoiseRT);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
			context.Submit();
		}
	}
}