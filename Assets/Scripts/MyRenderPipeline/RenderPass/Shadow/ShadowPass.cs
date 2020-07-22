using System.Collections.Generic;
using MyRenderPipeline.RenderPass;
using MyRenderPipeline.RenderPass.Shadow;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.Shadow
{
	[CreateAssetMenu(fileName = "ShadowPass", menuName = "MyRP/RenderPass/ShadowPass")]
	public class ShadowPass : MyRenderPassAsset
	{
		public override MyRenderPass CreateRenderPass()
		{
			return new ShadowPassRenderer(this);
		}
	}

	public partial class ShadowPassRenderer : MyRenderPassRenderer<ShadowPass>
	{
		private const int PassSimple = 0;
		private const int PassPSM = 1;
		private const int PassTSM = 2;

		private const string shaderName = "MyRP/Shadow/ShadowMap";

		public Dictionary<Light, MyShadowMapData> lightMaps = new Dictionary<Light, MyShadowMapData>();

		private Material shadowMapMat;
		private int defaultShadowMap;

		public ShadowPassRenderer(ShadowPass asset) : base(asset)
		{
		}
		
		protected override void Init()
		{
			shadowMapMat = new Material(Shader.Find(shaderName));
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			if (defaultShadowMap <= 0)
			{
				var cmd = CommandBufferPool.Get();
				defaultShadowMap = Shader.PropertyToID("_DefaultShadowMapTex");
				cmd.GetTemporaryRT(defaultShadowMap, 16, 16, 32, FilterMode.Point, RenderTextureFormat.Depth);
				cmd.SetRenderTarget(defaultShadowMap, defaultShadowMap);
				cmd.ClearRenderTarget(true, true, Color.black);

				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				CommandBufferPool.Release(cmd);
			}

			renderingData.defaultShadowMap = defaultShadowMap;
			lightMaps.Clear();
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			for (var i = 0; i < renderingData.cullResults.visibleLights.Length; ++i)
			{
				var light = renderingData.cullResults.visibleLights[i];
				if (light.light.GetComponent<ShadowSettings>() is ShadowSettings shadowSettings)
				{
					if (!shadowSettings.shadow)
						continue;

					MyShadowMapData data = new MyShadowMapData();
					var hasData = false;
					switch (shadowSettings.algorithms)
					{
						case ShadowAlgorithms.Standard:
							data = StandardShadowMap(context, renderingData, shadowSettings, i);
							hasData = true;
							break;
						case ShadowAlgorithms.PSM:
							data = PSMShadowMap(context, renderingData, shadowSettings, i);
							hasData = true;
							break;
						case ShadowAlgorithms.TSM:
							data = TSMShadowMap(context, renderingData, shadowSettings, i);
							hasData = true;
							break;
					}

					if (hasData)
					{
						renderingData.shadowMapData[light.light] = data;
						lightMaps[light.light] = data;
					}
				}
			}
		}

		private void DrawShadowCasters(ScriptableRenderContext context, MyRenderingData renderingData,
			MyShadowMapData shadowMapData, int pass)
		{
			var cmd = CommandBufferPool.Get();
			cmd.SetGlobalMatrix("_LightViewProjection", shadowMapData.world2Light);
			//如果用cullResults 则范围太小 , 一些摄像机外的阴影没有绘制
			foreach (var renderer in GameObject.FindObjectsOfType<UnityEngine.Renderer>())
			{
				cmd.DrawRenderer(renderer, shadowMapMat, 0, pass);
			}

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		public override void Cleanup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get();

			foreach (var light in renderingData.lights)
			{
				if (lightMaps.ContainsKey(light.light))
				{
					IdentifierPool.Release(lightMaps[light.light].shadowMapIdentifier);
					cmd.ReleaseTemporaryRT(lightMaps[light.light].shadowMapIdentifier);
				}
			}

			lightMaps.Clear();
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}


	}
}