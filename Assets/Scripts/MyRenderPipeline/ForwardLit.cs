﻿using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace MyRenderPipeline
{
	[CreateAssetMenu(fileName = "ForwardLit", menuName = "MyRP/RenderPass/ForwardLit")]
	public class ForwardLit : MyRenderPassAsset
	{
		public override MyRenderPass CreateRenderPass()
		{
			return new ForwardLitPass(this);
		}
	}

	public class ForwardLitPass : MyRenderPassRenderer<ForwardLit>
	{
		public ForwardLitPass(ForwardLit asset) : base(asset)
		{
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			SetupGlobalLight(context, ref renderingData);
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var camera = renderingData.camera;
			var cmd = CommandBufferPool.Get("RenderOpaque");
			using (new ProfilingSample(cmd, "RenderOpaque"))
			{
				//开始profilling  并且 清理之前可能没有处理的cmd
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				cmd.SetRenderTarget(renderingData.colorTarget, renderingData.depthTarget);
				cmd.SetViewProjectionMatrices(renderingData.viewMatrix, renderingData.jitteredProjectionMatrix);
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				var mainLightIndex = GetMainLightIndex(ref renderingData);

				// Render Main Light
				if (mainLightIndex >= 0)
				{
					RenderLight(context, ref renderingData, mainLightIndex, new ShaderTagId("ForwardBase"));
				}

				for (var i = 0; i < renderingData.cullResults.visibleLights.Length; i++)
				{
					if (i == mainLightIndex)
						continue;
					RenderLight(context, ref renderingData, i, new ShaderTagId("ForwardAdd"));
				}
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		void RenderLight(ScriptableRenderContext context, ref MyRenderingData renderingData, int lightIndex,
			ShaderTagId shaderTagId)
		{
			SetupLight(context, renderingData, lightIndex);

			FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
			SortingSettings sortingSettings = new SortingSettings(renderingData.camera);
			sortingSettings.criteria = SortingCriteria.CommonOpaque;
			DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings)
			{
				mainLightIndex = GetMainLightIndex(ref renderingData),
				enableDynamicBatching = false
			};
			RenderStateBlock stateBlock = new RenderStateBlock(RenderStateMask.Nothing);

			context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings,
				ref stateBlock);
		}

		private void SetupLight(ScriptableRenderContext context, MyRenderingData renderingData, int lightIndex)
		{
			var cmd = CommandBufferPool.Get();
			var light = renderingData.cullResults.visibleLights[lightIndex];
			if (light.lightType == LightType.Directional)
			{
				cmd.SetGlobalVector("_LightPosition", -light.light.transform.forward.ToVector4(0));
				cmd.SetGlobalColor("_LightColor", light.finalColor);
				cmd.SetGlobalVector("_LightDirection", -light.light.transform.forward);
				cmd.SetGlobalFloat("_LightCosHalfAngle", -2);
			}
			else
			{
				cmd.SetGlobalVector("_LightPosition", light.light.transform.position.ToVector4(1));
				cmd.SetGlobalColor("_LightColor", light.finalColor);
				cmd.SetGlobalVector("_LightDirection", -light.light.transform.forward.normalized);
				if (light.lightType == LightType.Spot)
					cmd.SetGlobalFloat("_LightCosHalfAngle", Mathf.Cos(Mathf.Deg2Rad * light.spotAngle / 2));
				else
					cmd.SetGlobalFloat("_LightCosHalfAngle", -2);
			}

			if (renderingData.shadowMapData.ContainsKey(light.light))
			{
				var shadowData = renderingData.shadowMapData[light.light];
				cmd.SetGlobalInt("_UseShadow", 1);
				cmd.SetGlobalMatrix("_WorldToLight", shadowData.world2Light);
				cmd.SetGlobalTexture("_ShadowMap", shadowData.shadowMapIdentifier);
				cmd.SetGlobalFloat("_ShadowBias", shadowData.bias);
				cmd.SetGlobalInt("_ShadowType", (int) shadowData.shadowType);
				cmd.SetGlobalVector("_ShadowParameters", shadowData.shadowParameters);
				cmd.SetGlobalMatrix("_ShadowPostTransform", shadowData.postTransform);
			}
			else
			{
				cmd.SetGlobalInt("_UseShadow", 0);
				cmd.SetGlobalMatrix("_WorldToLight", Matrix4x4.identity);
				cmd.SetGlobalTexture("_ShadowMap", renderingData.defaultShadowMap);
			}


			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}


		private void SetupGlobalLight(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get();
			var mainLightIdx = GetMainLightIndex(ref renderingData);
			if (mainLightIdx >= 0)
			{
				var mainLight = renderingData.lights[mainLightIdx];

				//mainLight dir 是要翻转下
				if (mainLight.light.type == LightType.Directional)
					cmd.SetGlobalVector("_MainLightPosition", -mainLight.light.transform.forward.ToVector4(0));
				else
					cmd.SetGlobalVector("_MainLightPosition", mainLight.light.transform.position.ToVector4(1));

				cmd.SetGlobalColor("_MainLightColor", mainLight.finalColor);
			}
			else
			{
				cmd.SetGlobalColor("_MainLightColor", Color.black);
				cmd.SetGlobalVector("_MainLightPosition", Vector4.zero);
			}

			cmd.SetGlobalColor("_AmbientLight", RenderSettings.ambientLight);
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		private int GetMainLightIndex(ref MyRenderingData renderingData)
		{
			var lights = renderingData.cullResults.visibleLights;
			var sun = RenderSettings.sun;
			if (sun == null)
			{
				for (var index = 0; index < lights.Length; index++)
				{
					var light = lights[index];
					if (light.light.type == LightType.Directional)
					{
						return index;
					}
				}
			}
			else
			{
				for (var index = 0; index < lights.Length; index++)
				{
					var light = lights[index];
					if (light.light == sun)
					{
						return index;
					}
				}
			}

			return -1;
		}
	}
}