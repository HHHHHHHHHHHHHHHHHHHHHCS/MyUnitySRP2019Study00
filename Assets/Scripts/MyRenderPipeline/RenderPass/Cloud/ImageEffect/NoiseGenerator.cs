using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class NoiseGenerator : MonoBehaviour
	{
		public const string detailNoiseName = "DetailNoise";
		public const string shapeNoiseName = "ShapeNoise";

		private const int computeThreadGroupSize = 8;

		private const int worley_Kernel = 0;
		private const int normalize_Kernel = 1;

		private static readonly int src_ID = Shader.PropertyToID("_Src");
		private static readonly int target_ID = Shader.PropertyToID("_Target");
		private static readonly int persistence_ID = Shader.PropertyToID("_Persistence");
		private static readonly int resolution_ID = Shader.PropertyToID("_Resolution");
		private static readonly int channelMask_ID = Shader.PropertyToID("_ChannelMask");
		private static readonly int minmax_ID = Shader.PropertyToID("_MinMax");
		private static readonly int result_ID = Shader.PropertyToID("_Result");


		private static readonly int pointsA_ID = Shader.PropertyToID("_PointsA");
		private static readonly int pointsB_ID = Shader.PropertyToID("_PointsB");
		private static readonly int pointsC_ID = Shader.PropertyToID("_PointsC");
		private static readonly int numCellsA_ID = Shader.PropertyToID("_NumCellsA");
		private static readonly int numCellsB_ID = Shader.PropertyToID("_NumCellsB");
		private static readonly int numCellsC_ID = Shader.PropertyToID("_NumCellsC");
		private static readonly int invertNoise_ID = Shader.PropertyToID("_InvertNoise");
		private static readonly int tile_ID = Shader.PropertyToID("_Tile");


		public enum CloudNoiseType
		{
			Shape,
			Detail
		}

		public enum TextureChannel
		{
			R,
			G,
			B,
			A
		}

		[Header("Editor Settings")] public CloudNoiseType activeTextureType;
		public TextureChannel activeChannel;
		public bool autoUpdate;
		public bool logComputeTime;

		[Header("Noise Settings")] public int shapeResolution = 132;
		public int detailResolution = 32;

		public WorleyNoiseSettings[] shapeSettings;
		public WorleyNoiseSettings[] detailSettings;
		public ComputeShader noiseCompute;
		public ComputeShader copy;

		[Header("Viewer Settings")] public bool viewerEnabled;
		public bool viewerGreyscale = true;
		public bool viewerShowAllChannels;
		[Range(0, 1)] public float viewerSliceDepth;
		[Range(1, 5)] public float viewerTileAmount = 1;
		[Range(0, 1)] public float viewerSize = 1;


		[HideInInspector] public bool showSettingsEditor = true;
		[SerializeField, HideInInspector] public RenderTexture shapeTexture;
		[SerializeField, HideInInspector] public RenderTexture detailTexture;

		// Internal
		private List<ComputeBuffer> buffersToRelease;
		private bool updateNoise;

		public RenderTexture ActiveTexture =>
			(activeTextureType == CloudNoiseType.Shape) ? shapeTexture : detailTexture;

		public WorleyNoiseSettings ActiveSettings
		{
			get
			{
				WorleyNoiseSettings[] settings =
					activeTextureType == CloudNoiseType.Shape ? shapeSettings : detailSettings;
				int activeChannelIndex = (int) activeChannel;
				if (activeChannelIndex >= settings.Length)
				{
					return null;
				}

				return settings[activeChannelIndex];
			}
		}

		public Vector4 ChannelMask => new Vector4(
			(activeChannel == TextureChannel.R) ? 1 : 0,
			(activeChannel == TextureChannel.G) ? 1 : 0,
			(activeChannel == TextureChannel.B) ? 1 : 0,
			(activeChannel == TextureChannel.A) ? 1 : 0
		);

		public void ManualUpdate()
		{
			updateNoise = true;
			UpdateNoise();
		}

		public void ActiveNoiseSettingsChanged()
		{
			if (autoUpdate)
			{
				updateNoise = true;
			}
		}

		public void UpdateNoise()
		{
			ValidateParamaters();

			CreateTexture(ref shapeTexture, shapeResolution, shapeNoiseName);
			CreateTexture(ref detailTexture, detailResolution, detailNoiseName);

			if (updateNoise && noiseCompute)
			{
				var timer = System.Diagnostics.Stopwatch.StartNew();

				updateNoise = false;
				var activeSettings = ActiveSettings;
				if (activeSettings == null)
				{
					return;
				}

				if (buffersToRelease == null)
				{
					buffersToRelease = new List<ComputeBuffer>();
				}
				else
				{
					buffersToRelease.Clear();
				}

				int activeTextureResolution = ActiveTexture.width;

				//set values:
				noiseCompute.SetFloat(persistence_ID, activeSettings.persistence);
				noiseCompute.SetInt(resolution_ID, activeTextureResolution);
				noiseCompute.SetVector(channelMask_ID, ChannelMask);

				//也可以用 noiseCompute.FindKernel("CSWorley");
				noiseCompute.SetTexture(worley_Kernel, result_ID, ActiveTexture);
				var minMaxBuffer = CreateBuffer(new int[] {int.MaxValue, 0}, sizeof(int),
					minmax_ID, worley_Kernel);
				UpdateWorley(activeSettings);

				//dispatch noise gen kernel
				//也可以用 noiseCompute.GetKernelThreadGroupSizes(0, out uint x, out uint y, out uint z);
				int numThreadGroups = Mathf.CeilToInt(activeTextureResolution / (float) computeThreadGroupSize);
				noiseCompute.Dispatch(worley_Kernel, numThreadGroups, numThreadGroups, numThreadGroups);


				//set normalization  
				noiseCompute.SetBuffer(normalize_Kernel, minmax_ID, minMaxBuffer);
				noiseCompute.SetTexture(normalize_Kernel, result_ID, ActiveTexture);
				//dispatch normalization
				noiseCompute.Dispatch(normalize_Kernel, numThreadGroups, numThreadGroups, numThreadGroups);

				if (logComputeTime)
				{
					//use getData can make sure wait really time
					var minMax = new int[2];
					minMaxBuffer.GetData(minMax);

					Debug.Log($"Noise Generation: {timer.ElapsedMilliseconds} ms");
				}

				//release buffers
				foreach (var buffer in buffersToRelease)
				{
					buffer.Release();
				}
			}
		}

		private void ValidateParamaters()
		{
			detailResolution = detailResolution > 1 ? detailResolution : 1;
			shapeResolution = shapeResolution > 1 ? shapeResolution : 1;
		}

		void CreateTexture(ref RenderTexture texture, int resolution, string textureName)
		{
			var format = GraphicsFormat.R16G16B16A16_UNorm;
			if (texture == null || !texture.IsCreated() || texture.width != resolution ||
			    texture.height != resolution || texture.volumeDepth != resolution || texture.graphicsFormat != format)
			{
				//Debug.Log ("Create tex: update noise: " + updateNoise);
				if (texture != null)
				{
					texture.Release();
				}

				texture = new RenderTexture(resolution, resolution, 0, format, 0)
				{
					name = textureName,
					volumeDepth = resolution,
					enableRandomWrite = true,
					dimension = TextureDimension.Tex3D,
					wrapMode = TextureWrapMode.Repeat,
					filterMode = FilterMode.Bilinear,
				};
				texture.Create();

				Load(name, texture);
			}
		}


		//读取本地出存的tex3d 赋值给target
		private void Load(string saveName, RenderTexture target)
		{
			string sceneName = SceneManager.GetActiveScene().name;
			saveName = sceneName + "_" + saveName;
			Texture3D saveTex = (Texture3D) Resources.Load(saveName);
			if (saveTex != null && saveTex.width == target.width)
			{
				copy.SetTexture(worley_Kernel, src_ID, saveTex);
				copy.SetTexture(worley_Kernel, target_ID, target);
				int numThreadGroups = Mathf.CeilToInt(saveTex.width / 8f);
				copy.Dispatch(worley_Kernel, numThreadGroups, numThreadGroups, numThreadGroups);
			}
		}

		//private ComputeBuffer CreateBuffer<T>(T[] data, int stride, int bufferName, int kernel = 0)
		private ComputeBuffer CreateBuffer(Array data, int stride, int bufferName, int kernel = 0)
		{
			var buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Structured);
			buffersToRelease.Add(buffer);
			buffer.SetData(data);
			noiseCompute.SetBuffer(kernel, bufferName, buffer);
			return buffer;
		}

		private void CreateWorleyPointsBuffer(Random prng, int numCellsPerAxis, int bufferName)
		{
			var points = new Vector3[numCellsPerAxis * numCellsPerAxis * numCellsPerAxis];
			float cellSize = 1f / numCellsPerAxis;

			for (int x = 0; x < numCellsPerAxis; x++)
			{
				for (int y = 0; y < numCellsPerAxis; y++)
				{
					for (int z = 0; z < numCellsPerAxis; z++)
					{
						float randomX = (float) prng.NextDouble(); //return 0~1
						float randomY = (float) prng.NextDouble();
						float randomZ = (float) prng.NextDouble();
						Vector3 randomOffset = new Vector3(randomX, randomY, randomZ) * cellSize;
						//逐渐增长
						Vector3 cellCorner = new Vector3(x, y, z) * cellSize;

						int index = x + numCellsPerAxis * (y + z * numCellsPerAxis);
						points[index] = cellCorner + randomOffset;
					}
				}
			}

			CreateBuffer(points, sizeof(float) * 3, bufferName, worley_Kernel);
		}

		private void UpdateWorley(WorleyNoiseSettings settings)
		{
			var prng = new Random(settings.seed);
			CreateWorleyPointsBuffer(prng, settings.numDivisionsA, pointsA_ID);
			CreateWorleyPointsBuffer(prng, settings.numDivisionsB, pointsB_ID);
			CreateWorleyPointsBuffer(prng, settings.numDivisionsC, pointsC_ID);

			noiseCompute.SetInt(numCellsA_ID, settings.numDivisionsA);
			noiseCompute.SetInt(numCellsB_ID, settings.numDivisionsB);
			noiseCompute.SetInt(numCellsC_ID, settings.numDivisionsC);
			noiseCompute.SetBool(invertNoise_ID, settings.invert);
			noiseCompute.SetInt(tile_ID, settings.tile);
		}
	}
}