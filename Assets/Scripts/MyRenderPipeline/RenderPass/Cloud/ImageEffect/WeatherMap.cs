using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class WeatherMap : MonoBehaviour
	{
		public bool logTime;
		public ComputeShader noiseCompute;
		public SimplexNoiseSettings noiseSettings;
		public int resolution = 512;
		public RenderTexture weatherMap;
		public Vector4 testParams;
		public Transform container;

		public bool viewerEnabled;
		[HideInInspector] public bool showSettingsEditor = true;

		List<ComputeBuffer> buffersToRelease;

		public Vector2 minMax = new Vector2(0, 1);
		//public int[] minMaxTest;
		
		public void UpdateMap()
		{
			var sw = System.Diagnostics.Stopwatch.StartNew();
			
			CreateTexture(ref weatherMap,resolution);
			
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