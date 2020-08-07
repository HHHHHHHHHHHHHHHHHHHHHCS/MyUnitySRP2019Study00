using System.Collections.Generic;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace MyRenderPipeline.RenderPass.VolumeLight
{
	[CreateAssetMenu(fileName = "VolumeLight", menuName = "MyRP/RenderPass/VolumeLight")]
	public class VolumeLightPass : MyRenderPassAsset
	{
		public int volumeResolutionScale = 2;
		public float visibilityDistance = 20;

		[ColorUsage(false, true)] public Color fogLight;
		public Material material;
		public Texture2D[] jitterPatterns;

		public override MyRenderPass CreateRenderPass()
		{
			return new VolumeLightRender(this);
		}
	}

	public class VolumeLightRender : MyRenderPassRenderer<VolumeLightPass>
	{
		private struct LightVolumeData
		{
			public VolumeLightRenderer volume;
			public int lightIndex;
			public int volumeIndex;
		}

		private const int volumeDepthPass = 0;
		private const int volumeScatteringPass = 1;
		private const int fullScreenVolumeScatteringPass = 2;
		private const int volumeResolvePass = 3;
		private const int globalFogPass = 4;

		private List<LightVolumeData> visibleVolumes = new List<LightVolumeData>();
		private int volumeDepthTexID = -1;

		private Material volumeMat => asset.material;

		public VolumeLightRender(VolumeLightPass asset) : base(asset)
		{
			volumeDepthTexID = Shader.PropertyToID("_VolumeDepthTexture");
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			visibleVolumes.Clear();

			for (int i = 0; i < renderingData.cullResults.visibleLights.Length; i++)
			{
				var light = renderingData.cullResults.visibleLights[i];
				VolumeLightRenderer vlr = light.light.GetComponent<VolumeLightRenderer>();
				if (vlr)
				{
					visibleVolumes.Add(new LightVolumeData()
					{
						lightIndex = i,
						volumeIndex = visibleVolumes.Count,
						volume = vlr,
					});
				}
			}

			// 第一帧数 初始化
			if (renderingData.frameID == 0)
			{
				var cmd = CommandBufferPool.Get();
				cmd.SetGlobalVectorArray("_BoundaryPlanes", new Vector4[6]);
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				CommandBufferPool.Release(cmd);
			}
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get("Volume Light");

			using (new ProfilingSample(cmd, "Volume Light"))
			{
				RenderVolumeLight(cmd, renderingData);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				CommandBufferPool.Release(cmd);
			}
		}

		//Debug 用
		private void RenderVolumeDepth(CommandBuffer cmd, MyRenderingData renderingData)
		{
			var debugRT = IdentifierPool.Get();
			cmd.GetTemporaryRT(debugRT, renderingData.camera.pixelWidth, renderingData.camera.pixelHeight, 0,
				FilterMode.Point, RenderTextureFormat.ARGBFloat);
			cmd.SetRenderTarget(debugRT);
			cmd.SetGlobalTexture("_RWVolumeDepthTexture", volumeDepthTexID);

			foreach (var volumeData in visibleVolumes)
			{
				cmd.SetGlobalInt("_VolumeIndex", volumeData.volumeIndex);
				cmd.DrawMesh(volumeData.volume.VolumeMesh, volumeData.volume.transform.localToWorldMatrix, volumeMat, 0,
					volumeDepthPass);
			}

			cmd.ReleaseTemporaryRT(debugRT);
			IdentifierPool.Release(debugRT);
		}


		private void RenderVolumeLight(CommandBuffer cmd, MyRenderingData renderingData)
		{
			var rendererSize = new Vector2Int(renderingData.camera.pixelWidth, renderingData.camera.pixelHeight) /
			                   asset.volumeResolutionScale;
			var rt = IdentifierPool.Get();
			cmd.GetTemporaryRT(rt, rendererSize.x, rendererSize.y, 0, FilterMode.Point, RenderTextureFormat.Default);
			cmd.SetRenderTarget(rt, rt);
			cmd.ClearRenderTarget(false, true, Color.black);

			cmd.SetGlobalTexture("_CameraDepthTex", renderingData.depthTarget);
			cmd.SetCameraParams(renderingData.camera, true);

			float globalExtinction = Mathf.Log(10) / asset.visibilityDistance; //全局 每单位距离衰减

			foreach (var volumeData in visibleVolumes)
			{
				if (!volumeData.volume.enabled)
				{
					return;
				}

				var light = renderingData.cullResults.visibleLights[volumeData.lightIndex];
				Vector4 lightPos;
				if (light.lightType == LightType.Directional)
				{
					lightPos = (-light.light.transform.forward).ToVector4(0);
				}
				else
				{
					lightPos = light.light.transform.position.ToVector4(1);
				}

				cmd.SetGlobalVector("_LightPosition", lightPos);
				cmd.SetGlobalVector("_LightDirection", -light.light.transform.forward);
				cmd.SetGlobalFloat("_LightAngle", Mathf.Cos(Mathf.Deg2Rad * light.spotAngle / 2));
				cmd.SetGlobalVector("_LightColor", light.finalColor * volumeData.volume.intensityMultiplier);
				cmd.SetGlobalVector("_WorldCameraPos", renderingData.camera.transform.position);
				cmd.SetGlobalVector("_FrameSize",
					new Vector4(rendererSize.x, rendererSize.y, 1f / rendererSize.x, 1f / rendererSize.y));
				cmd.SetGlobalInt("_Steps", volumeData.volume.rayMarchingSteps);
				cmd.SetGlobalVector("_RangeLimit", volumeData.volume.rayMarchingRange);
				cmd.SetGlobalFloat("_IncomingLoss", volumeData.volume.incomingLoss);
				cmd.SetGlobalFloat("_LightDistance", volumeData.volume.lightDistance);
				var extinction = globalExtinction;
				if (volumeData.volume.extinctionOverride)
				{
					extinction = Mathf.Log(10) / volumeData.volume.visibilityDistance;
				}

				cmd.SetGlobalVector("_TransmittanceExtinction", new Vector3(extinction, extinction, extinction));
				if (asset.jitterPatterns.Length > 0)
				{
					cmd.SetGlobalTexture("_SampleNoise",
						asset.jitterPatterns[renderingData.frameID % asset.jitterPatterns.Length]);
				}

				if (renderingData.shadowMapData.TryGetValue(volumeData.volume.TheLight, out var shadowData))
				{
					cmd.SetGlobalTexture("_ShadowMap", shadowData.shadowMapIdentifier);
					cmd.SetGlobalMatrix("_WorldToLight", shadowData.world2Light);
					cmd.SetGlobalFloat("_ShadowBias", shadowData.bias);
					cmd.SetGlobalInt("_ShadowType", (int) shadowData.shadowType);
					cmd.SetGlobalVector("_ShadowParameters", shadowData.shadowParameters);
					cmd.SetGlobalMatrix("_ShadowPostTransform", shadowData.postTransform);
					cmd.SetGlobalInt("_UseShadow", 1);
				}
				else
				{
					cmd.SetGlobalInt("_UseShadow", 0);
				}

				var boundaryPlanes = volumeData.volume.GetVolumeBoundFaces(renderingData.camera);
				cmd.SetGlobalVectorArray("_BoundaryPlanes", boundaryPlanes);
				cmd.SetGlobalInt("_BoundaryPlaneCount", boundaryPlanes.Count);

				switch (light.lightType)
				{
					case LightType.Point:
					case LightType.Spot:
						cmd.DrawMesh(volumeData.volume.VolumeMesh, volumeData.volume.transform.localToWorldMatrix,
							volumeMat, 0, volumeScatteringPass);
						break;
					case LightType.Directional:
						cmd.BlitFullScreen(BuiltinRenderTextureType.None, rt, volumeMat,
							fullScreenVolumeScatteringPass);
						break;
				}
			}

			cmd.SetGlobalTexture("_CameraDepthTex", renderingData.depthTarget);
			cmd.SetGlobalFloat("_GlobalFogExtinction", globalExtinction);
			cmd.SetGlobalColor("_AmbientLight", asset.fogLight);
			cmd.BlitFullScreen(BuiltinRenderTextureType.None, renderingData.colorTarget, volumeMat, globalFogPass);

			cmd.Blit(rt, renderingData.colorTarget, volumeMat, volumeResolvePass);

			cmd.ReleaseTemporaryRT(rt);
			IdentifierPool.Release(rt);
		}
	}
}