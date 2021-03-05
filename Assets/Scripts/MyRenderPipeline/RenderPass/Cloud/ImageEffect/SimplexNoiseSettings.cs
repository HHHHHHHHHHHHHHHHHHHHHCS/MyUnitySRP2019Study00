using System;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	[CreateAssetMenu]
	public class SimplexNoiseSettings : NoiseSettings
	{
		public struct DataStruct
		{
			public int seed;
			public int numLayers;
			public float scale;
			public float lacunarity;
			public float persistence;
			public Vector2 offset;
		}

		public int seed;
		[Range(1, 6)] public int numLayers = 1;
		public float scale = 1;
		public float lacunarity = 2;
		public float persistence = 0.5f;
		public Vector2 offset;

		public override int Stride => 7 * sizeof(float);


		public override Array GetDataArray()
		{
			var data = new DataStruct()
			{
				seed = seed,
				numLayers = Mathf.Max(1, numLayers),
				scale = scale,
				lacunarity = lacunarity,
				persistence = persistence,
				offset = offset
			};

			return new[] {data};
		}
	}
}