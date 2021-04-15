using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	//isframe   0帧数 uv.x<=0.5    1帧数 uv.x>=0.5   2帧数不更新
	//mulrt  0rt blend 用	1rt 1/2   2rt  1/4
	//todo:frame mode 0 可以画一个更小的RT 然后 合并上去
	//todo: 添加quarter 
	public class SolidCloudRenderPass : ScriptableRenderPass
	{
		// private const int c_MulRTStart = 1;

		private const string k_SolidCloudPass = "SolidCloud";

		private const string k_CLOUD_MASK = "CLOUD_MASK";

		private const string k_CLOUD_USE_XY_PLANE = "CLOUD_USE_XY_PLANE";

		// private const string k_CLOUD_AREA_BOX = "CLOUD_AREA_BOX";
		private const string k_CLOUD_AREA_SPHERE = "CLOUD_AREA_SPHERE";
		private const string k_CLOUD_SUN_SHADOWS_ON = "CLOUD_SUN_SHADOWS_ON";
		private const string k_CLOUD_DISTANCE_ON = "CLOUD_DISTANCE_ON";
		private const string k_CLOUD_BLUR_ON = "CLOUD_BLUR_ON";
		private const string k_CLOUD_MUL_RT_ON = "CLOUD_MUL_RT_ON";
		private const string k_CLOUD_FRAME_ON = "CLOUD_FRAME_ON";

		private static readonly string[] k_FRAME_MODE =
		{
			"FRAME_MODE_0", "FRAME_MODE_1", "FRAME_MODE_2", "FRAME_MODE_3"
		};


		private static readonly int GenNoiseTex_ID = Shader.PropertyToID("_GenNoiseTex");
		private static readonly int RandomNoiseTex_ID = Shader.PropertyToID("_RandomNoiseTex");
		private static readonly int BlendTex_ID = Shader.PropertyToID("_BlendTex");

		private static readonly int[] TempBlendTex_ID =
		{
			Shader.PropertyToID("_TempBlendTex0"),
			Shader.PropertyToID("_TempBlendTex1"),
			Shader.PropertyToID("_TempBlendTex2"),
		};


		public static readonly int inverseViewAndProjectionMatrix = Shader.PropertyToID("unity_MatrixInvVP");


		private static readonly RenderTargetIdentifier CameraColorTexture_RTI =
			new RenderTargetIdentifier("_CameraColorTexture");

		private static readonly RenderTargetIdentifier GenNoiseTex_RTI =
			new RenderTargetIdentifier(GenNoiseTex_ID);

		private static readonly RenderTargetIdentifier RandomNoiseTex_RTI =
			new RenderTargetIdentifier(RandomNoiseTex_ID);

		private static readonly RenderTargetIdentifier BlendTex_RTI =
			new RenderTargetIdentifier(BlendTex_ID);

		private static readonly RenderTargetIdentifier[] TempBlendTex_RTI =
		{
			new RenderTargetIdentifier(TempBlendTex_ID[0]),
			new RenderTargetIdentifier(TempBlendTex_ID[1]),
			new RenderTargetIdentifier(TempBlendTex_ID[2]),
		};

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
		private static readonly int Frame_ID = Shader.PropertyToID("_Frame");
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
		private RenderTexture[] frameRTS;

		private int rtSize, screenWidth, screenHeight, noiseSize;
		private int mainLightIndex;
		private bool mulRTBlend, enableFrame;
		private int frameMode;

		private Vector3 windSpeedAcum = Vector3.zero;
		private float amount = 0;
		private int frame = 0;


		public void Init(Material solidCloudMaterial, Texture2D noiseTex)
		{
			this.solidCloudMaterial = solidCloudMaterial;
			this.noiseTex = noiseTex;
		}

		public void Setup(SolidCloudRenderPostProcess solidCloudRenderPostProcess, int width, int height)
		{
			settings = solidCloudRenderPostProcess;
			rtSize = settings.rtSize.value;
			screenWidth = width;
			screenHeight = height;
			noiseSize = 2 << settings.noisePowSize.value;
			mulRTBlend = settings.mulRTBlend.value;
			enableFrame = settings.enableFrame.value;
			frameMode = settings.frameMode.value;


			if (enableFrame)
			{
				if (CheckFrameRTChange())
				{
					Destroy();
				}

				CreateFrameRT();
			}
			else
			{
				Destroy();
			}
		}

		public void Destroy()
		{
			if (frameRTS != null)
			{
				foreach (var rt in frameRTS)
				{
					if (rt != null)
					{
						Object.DestroyImmediate(rt);
					}
				}
			}

			frameRTS = null;
		}

		private bool CheckFrameRTChange()
		{
			if (frameRTS != null)
			{
				if (mulRTBlend)
				{
					if (frameRTS.Length != 3)
					{
						return true;
					}
				}
				else
				{
					if (frameRTS.Length != 1)
					{
						return true;
					}
				}

				int div = (int) Mathf.Pow(2, rtSize - 1);


				int width = screenWidth / div;
				int height = screenHeight / div;
				if (frameRTS[0] == null || frameRTS[0].width != width || frameRTS[0].height != height)
				{
					return true;
				}
			}

			return false;
		}

		private void CreateFrameRT()
		{
			if (frameRTS != null)
			{
				return;
			}

			int len = mulRTBlend ? 3 : 1;
			frameRTS = new RenderTexture[len];
			int div = (int) Mathf.Pow(2, rtSize - 1);

			for (int i = 0; i < len; i++)
			{
				if (mulRTBlend && i != 0)
				{
					div <<= 1;
				}

				int width = screenWidth / div;
				int height = screenHeight / div;
				frameRTS[i] = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32,
					RenderTextureReadWrite.sRGB)
				{
					filterMode = FilterMode.Bilinear
				};
			}
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get(k_SolidCloudPass);
			using (new ProfilingScope(cmd, profilingSampler))
			{
				mainLightIndex = renderingData.lightData.mainLightIndex;

				GenNoise(context, cmd, ref renderingData.lightData);
				RandomNoise(context, cmd);
				RaymarchCloud(context, cmd, ref renderingData);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				cmd.ReleaseTemporaryRT(GenNoiseTex_ID);
				cmd.ReleaseTemporaryRT(RandomNoiseTex_ID);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		private void GenNoise(ScriptableRenderContext context, CommandBuffer cmd, ref LightData lightDatas)
		{
			//gen noise 
			//###############################################
			//也可以用compute shader 代替
			//可以预烘焙 只生成一次
			cmd.SetGlobalTexture(NoiseTex_ID, noiseTex);
			solidCloudMaterial.SetFloat(NoiseStrength_ID, settings.noiseStrength.value);
			solidCloudMaterial.SetFloat(NoiseDensity_ID, settings.noiseDensity.value);
			Vector3 mainLightDir;

			Color mainLightColor;
			if (mainLightIndex >= 0)
			{
				var lightData = lightDatas.visibleLights[mainLightIndex];
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
			int nz = Mathf.FloorToInt(nlight.z * noiseSize) * noiseSize;
			int noiseSeed = (int) (nz + nlight.x * noiseSize) + noiseSize * noiseSize;
			solidCloudMaterial.SetInt(NoiseSize_ID, noiseSize);
			solidCloudMaterial.SetInt(NoiseCount_ID, noiseSize * noiseSize);
			solidCloudMaterial.SetInt(NoiseSeed_ID, noiseSeed);
			cmd.GetTemporaryRT(GenNoiseTex_ID, noiseSize, noiseSize, 0, FilterMode.Bilinear,
				RenderTextureFormat.ARGB32);
			cmd.SetRenderTarget(GenNoiseTex_RTI, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 1);
			cmd.SetGlobalTexture(NoiseTex_ID, GenNoiseTex_RTI);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
		}

		private void RandomNoise(ScriptableRenderContext context, CommandBuffer cmd)
		{
			//random noise 
			//###############################################
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


			amount += Time.deltaTime * settings.noiseSpeed.value;
			solidCloudMaterial.SetFloat(Amount_ID, amount);
			cmd.GetTemporaryRT(RandomNoiseTex_ID, noiseSize, noiseSize, 0, FilterMode.Bilinear,
				RenderTextureFormat.ARGB32);
			cmd.SetRenderTarget(RandomNoiseTex_RTI, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
			CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 2);
			cmd.SetGlobalTexture(NoiseTex_ID, RandomNoiseTex_RTI);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
		}

		private void RaymarchCloud(ScriptableRenderContext context, CommandBuffer cmd, ref RenderingData renderingData)
		{
			//raymarch cloud
			//###############################################

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

			//URP 7.5.3之后  cameraData.GetGPUProjectionMatrix() 进行y翻转
			var cameraData = renderingData.cameraData;
			if (cameraData.IsCameraProjectionMatrixFlipped())
			{
				Matrix4x4 viewAndProjectionMatrix =
					GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(), false) * cameraData.GetViewMatrix();
				Matrix4x4 inverseViewProjection = Matrix4x4.Inverse(viewAndProjectionMatrix);
				cmd.SetGlobalMatrix(inverseViewAndProjectionMatrix, inverseViewProjection);
			}


			if (enableFrame)
			{
				CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_FRAME_ON, true);
				if (frameMode == 2)
				{
					frame = (frame + 1) % 3;
				}
				else
				{
					frame = (frame + 1) % 4;
				}

				solidCloudMaterial.SetInt(Frame_ID, frame);
				for (int i = 0; i < k_FRAME_MODE.Length; i++)
				{
					CoreUtils.SetKeyword(solidCloudMaterial, k_FRAME_MODE[i], i == frameMode);
				}
			}
			else
			{
				CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_FRAME_ON, false);
				frame = -1;
			}
			
			CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_MUL_RT_ON, mulRTBlend);

			//如果是mul叠加  则 用下面两级来叠加 1/2 1/4 1/8 1/16  步长则最短
			if (mulRTBlend)
			{
				cmd.SetGlobalInt(DstBlend_ID, (int) BlendMode.Zero);

				//blend
				for (int i = 1; i < 3; i++)
				{
					if (enableFrame)
					{
						if (frameMode == 2 && frame == 2)
						{
							break;
						}

						cmd.SetRenderTarget(frameRTS[i], RenderBufferLoadAction.DontCare,
							RenderBufferStoreAction.Store,
							RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
						//frame 叠加   如果clear了  覆盖不上去   而且还少了一个clear
						// CoreUtils.ClearRenderTarget(cmd, ClearFlag.None, new Color(0, 0, 0, 0));
						CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
						cmd.SetGlobalTexture(TempBlendTex_ID[i], frameRTS[i]);
					}
					else
					{
						int div = (int) Math.Pow(2, rtSize - 1 + i);
						int width = screenWidth / div;
						int height = screenHeight / div;

						//其实FPS够高 cmd.GetTemporaryRT 也有效果  但是我这里为了保险用了常驻的rt
						cmd.GetTemporaryRT(TempBlendTex_ID[i], width, height, 0, FilterMode.Bilinear,
							RenderTextureFormat.ARGB32);
						cmd.SetRenderTarget(TempBlendTex_RTI[i], RenderBufferLoadAction.DontCare,
							RenderBufferStoreAction.Store,
							RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
						//frame 叠加   如果clear了  覆盖不上去   而且还少了一个clear
						// CoreUtils.ClearRenderTarget(cmd, ClearFlag.Color, new Color(0, 0, 0, 0));
						CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
						cmd.SetGlobalTexture(TempBlendTex_ID[i], TempBlendTex_RTI[i]);
					}
				}

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				if (!enableFrame && !settings.enableBlur.value)
				{
					cmd.SetGlobalInt(DstBlend_ID,
						(int) (settings.enableBlend.value ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
					cmd.SetRenderTarget(CameraColorTexture_RTI);
					CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 4);
				}
				else
				{
					if (enableFrame)
					{
						if (!(frameMode == 2 && frame == 2))
						{
							cmd.SetRenderTarget(frameRTS[0], RenderBufferLoadAction.Load,
								RenderBufferStoreAction.Store,
								RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
							CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 4);
							cmd.SetGlobalTexture(BlendTex_ID, frameRTS[0]);

							context.ExecuteCommandBuffer(cmd);
							cmd.Clear();
						}
					}
					else
					{
						int div = (int) Math.Pow(2, rtSize - 1);
						int width = screenWidth / div;
						int height = screenHeight / div;
						cmd.GetTemporaryRT(BlendTex_ID, width, height, 0, FilterMode.Bilinear,
							RenderTextureFormat.ARGB32);
						cmd.SetRenderTarget(BlendTex_RTI, RenderBufferLoadAction.DontCare,
							RenderBufferStoreAction.Store,
							RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
						CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 4);
						cmd.SetGlobalTexture(BlendTex_ID, BlendTex_RTI);

						context.ExecuteCommandBuffer(cmd);
						cmd.Clear();

						for (int i = 1; i < 3; i++)
						{
							cmd.ReleaseTemporaryRT(TempBlendTex_ID[i]);
						}
					}


					CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_BLUR_ON, settings.enableBlur.value);
					cmd.SetGlobalInt(DstBlend_ID,
						(int) (settings.enableBlend.value ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));

					cmd.SetRenderTarget(CameraColorTexture_RTI);
					CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 3);
				}

				cmd.ReleaseTemporaryRT(BlendTex_ID);
			}
			else
			{
				if (rtSize == 1 && settings.enableBlur.value == false && enableFrame == false)
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
					if (enableFrame)
					{
						if (!(frameMode == 2 && frame == 2))
						{
							cmd.SetGlobalInt(DstBlend_ID, (int) BlendMode.Zero);
							cmd.SetRenderTarget(frameRTS[0], RenderBufferLoadAction.DontCare,
								RenderBufferStoreAction.Store);
							CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
							cmd.SetGlobalTexture(BlendTex_ID, frameRTS[0]);
						}
					}
					else
					{
						int div = (int) Mathf.Pow(2, rtSize - 1);
						int width = screenWidth / div;
						int height = screenHeight / div;

						cmd.GetTemporaryRT(BlendTex_ID, width, height, 0, FilterMode.Bilinear,
							RenderTextureFormat.ARGB32);
						cmd.SetRenderTarget(BlendTex_RTI, RenderBufferLoadAction.DontCare,
							RenderBufferStoreAction.Store,
							RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
						CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 0);
						cmd.SetGlobalTexture(BlendTex_ID, BlendTex_RTI);
					}


					context.ExecuteCommandBuffer(cmd);
					cmd.Clear();


					CoreUtils.SetKeyword(solidCloudMaterial, k_CLOUD_BLUR_ON, settings.enableBlur.value);
					cmd.SetGlobalInt(DstBlend_ID,
						(int) (settings.enableBlend.value ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
					cmd.SetRenderTarget(CameraColorTexture_RTI);
					CoreUtils.DrawFullScreen(cmd, solidCloudMaterial, null, 3);

					cmd.ReleaseTemporaryRT(BlendTex_ID);
				}
			}

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
		}
	}
}