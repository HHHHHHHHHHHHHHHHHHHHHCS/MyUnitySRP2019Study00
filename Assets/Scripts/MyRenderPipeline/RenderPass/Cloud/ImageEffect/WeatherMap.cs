using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class WeatherMap : MonoBehaviour
	{
		private const int main_Kernel = 0;
		private const int normalize_Kernel = 1;
		private const int threadGroupSize = 16;


		private static readonly int offsets_ID = Shader.PropertyToID("_Offsets");
		private static readonly int noiseSettings_ID = Shader.PropertyToID("_NoiseSettings");
		private static readonly int minMaxBuffer_ID = Shader.PropertyToID("_MinMaxBuffer");
		private static readonly int result_ID = Shader.PropertyToID("_Result");
		private static readonly int resolution_ID = Shader.PropertyToID("_Resolution");
		private static readonly int minMax_ID = Shader.PropertyToID("_MinMax");
		private static readonly int params_ID = Shader.PropertyToID("_Params");


		public bool logTime;
		public ComputeShader noiseCompute;
		public SimplexNoiseSettings noiseSettings;
		public int resolution = 512;
		public RenderTexture weatherMap;
		public Vector4 testParams;
		public Transform container;

		public bool viewerEnabled;

#if UNITY_EDITOR
		[HideInInspector] public bool showSettingsEditor = true;
#endif

		public Vector2 minMax = new Vector2(0, 1);
		//public int[] minMaxTest;

		private List<ComputeBuffer> buffersToRelease;


		public void UpdateMap(Vector2 heightOffset)
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();

			CreateTexture(ref weatherMap, resolution);

			if (noiseCompute == null)
			{
				return;
			}

			buffersToRelease = new List<ComputeBuffer>();

			var prng = new System.Random(noiseSettings.seed);
			var offsets = new Vector4[noiseSettings.numLayers];
			for (int i = 0; i < offsets.Length; i++)
			{
				var o = new Vector4((float) prng.NextDouble(), (float) prng.NextDouble(), (float) prng.NextDouble(),
					(float) prng.NextDouble());
				offsets[i] = (o * 2 - Vector4.one) * 1000 + (Vector4) container.position;
			}

			CreateBuffer(offsets, sizeof(float) * 4, offsets_ID);

			var settings = (SimplexNoiseSettings.DataStruct) noiseSettings.GetDataArray().GetValue(0);
			settings.offset += heightOffset;
			CreateBuffer(new SimplexNoiseSettings.DataStruct[] {settings}, noiseSettings.Stride, noiseSettings_ID,
				main_Kernel);
			noiseCompute.SetTexture(0, result_ID, weatherMap);
			noiseCompute.SetInt(resolution_ID, resolution);
			var minMaxBuffer = CreateBuffer(new int[] {int.MaxValue, 0}, sizeof(int), minMaxBuffer_ID, main_Kernel);
			noiseCompute.SetVector(minMax_ID, minMax);
			noiseCompute.SetVector(params_ID, testParams);

			int numThreadGroups = Mathf.CeilToInt(resolution / (float) threadGroupSize);
			noiseCompute.Dispatch(0, numThreadGroups, numThreadGroups, 1);

			// noiseCompute.SetBuffer(normalize_Kernel, minMaxBuffer_ID, minMaxBuffer);
			// noiseCompute.SetTexture(normalize_Kernel, result_ID, weatherMap);
			//noiseCompute.Dispatch (normalize_Kernel, numThreadGroups, numThreadGroups, normalize_Kernel);

			//minMaxTest = new int[2];
			//minMaxBuffer.GetData (minMaxTest);

			// Release buffers
			foreach (var buffer in buffersToRelease)
			{
				buffer.Release();
			}

			if (logTime)
			{
				Debug.Log("Weather gen: " + sw.ElapsedMilliseconds + " ms.");
			}
		}

		ComputeBuffer CreateBuffer(System.Array data, int stride, int bufferID, int kernel = 0)
		{
			var buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Raw);
			buffersToRelease.Add(buffer);
			buffer.SetData(data);
			noiseCompute.SetBuffer(kernel, bufferID, buffer);
			return buffer;
		}


		private void CreateTexture(ref RenderTexture texture, int resolution)
		{
			var format = GraphicsFormat.R16G16B16A16_UNorm;
			if (texture == null || !texture.IsCreated() || texture.width != resolution || texture.height != resolution
			    || texture.graphicsFormat != format)
			{
				if (texture != null)
				{
					texture.Release();
				}

				texture = new RenderTexture(resolution, resolution, 0, format, 0)
				{
					name = gameObject.name,
					volumeDepth = resolution,
					enableRandomWrite = true,
					dimension = TextureDimension.Tex2D,
					wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Bilinear,
				};

				texture.Create();
			}
		}
	}
}