using System.Collections.Generic;
using System.Linq;
using MyRenderPipeline.RenderPass.Common;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.TAA
{
	public enum SamplingPatterns
	{
		Halton2_3,
		Uniform,
	}

	[CreateAssetMenu(fileName = "TAAPass", menuName = "MyRP/RenderPass/TAAPass")]
	public class TAAPass : MyRenderPassAsset
	{
		public SamplingPatterns SamplingPatterns;
		public int Samples = 4;
		[Range(0, 1)] public float BlendAlpha = 0.1f;

		public override MyRenderPass CreateRenderPass()
		{
			return new TAARenderer(this);
		}
	}

	public class TAARenderer : MyRenderPassRenderer<TAAPass>
	{
		enum HistoricalBuffer : int
		{
			Color = 1,
			Depth,
			Velocity,
		}

		public static Vector2[] Pattern4 = new Vector2[]
		{
			new Vector2(.25f, .25f),
			new Vector2(.75f, .25f),
			new Vector2(.75f, .75f),
			new Vector2(.25f, .75f),
		};

		private List<Vector2> patterns = new List<Vector2>(16);

		private HistoricalRTSystem HistoricalRT = new HistoricalRTSystem();
		private Material material;
		private int previousColor;


		public TAARenderer(TAAPass asset) : base(asset)
		{
		}

		protected override void Init()
		{
			material = new Material(Shader.Find("MyRP/TAA"));
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			if (patterns.Capacity < asset.Samples)
				patterns.Capacity = asset.Samples;

			if (asset.SamplingPatterns == SamplingPatterns.Uniform)
			{
				asset.Samples = Mathf.ClosestPowerOfTwo(asset.Samples);
				var size = Mathf.Sqrt(asset.Samples);
				patterns.Clear();
				for (int y = 0; y < Mathf.Sqrt(asset.Samples); y++)
				{
					for (int x = 0; x < Mathf.Sqrt(asset.Samples); x++)
					{
						patterns.Add(new Vector2(x / size + .5f * size, y / size + .5f * size));
					}
				}
			}
			else if (asset.SamplingPatterns == SamplingPatterns.Halton2_3)
			{
				//linq 配合 迭代器 可以拿到指定数量的
				patterns = Sampler.HaltonSequence2(2, 3).Skip(1).Take(asset.Samples).ToList();
			}

			renderingData.nextProjectionJitter = patterns[renderingData.frameID % asset.Samples];

			HistoricalRT.Swap();
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get("TAA Resolve");
			var (previousColor, nextColor) = GetHistoricalColorBuffer(renderingData);

			cmd.SetGlobalTexture("_PreviousFrameBuffer", previousColor);
			cmd.SetGlobalTexture("_CurrentFrameBuffer", renderingData.colorTarget);
			cmd.SetGlobalFloat("_Alpha", asset.BlendAlpha);
			cmd.SetGlobalTexture("_VelocityBuffer", renderingData.velocityBuffer);
			cmd.Blit(renderingData.colorTarget, nextColor, material, 0);
			cmd.Blit(nextColor, renderingData.colorTarget);

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		(RenderTexture previous, RenderTexture next) GetHistoricalColorBuffer(MyRenderingData renderingData)
		{
			//局部方法
			RenderTexture allocator()
			{
				var rt = new RenderTexture(renderingData.camera.pixelWidth, renderingData.camera.pixelHeight, 0);
				rt.dimension = TextureDimension.Tex2D;
				rt.antiAliasing = 1;
				rt.format = renderingData.colorBufferFormat;
				rt.filterMode = FilterMode.Bilinear;
				rt.memorylessMode = RenderTextureMemoryless.None;
				rt.Create();
				return rt;
			}

			return (HistoricalRT.GetPrevious((int) HistoricalBuffer.Color, allocator),
				HistoricalRT.GetNext((int) HistoricalBuffer.Color, allocator));
		}


		public override void Cleanup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
		}
	}
}