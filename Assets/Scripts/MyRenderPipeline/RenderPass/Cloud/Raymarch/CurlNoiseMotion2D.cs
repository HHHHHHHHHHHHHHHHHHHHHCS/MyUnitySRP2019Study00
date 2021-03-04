using MyRenderPipeline.Utility;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.Raymarch
{
	[ExecuteInEditMode]
	public class CurlNoiseMotion2D : MonoBehaviour
	{
		public ComputeShader motionComputeShader;
		public RenderTexture curlNoise;
		public bool dynamicUpdate = false;
		public float speed = 1;

		public bool debug = false;

		private RenderTexture[] motionTextures;

		private int currentIdx;

		public RenderTexture currentMotionTexture =>
			motionTextures.Length >= 2 ? motionTextures[currentIdx % 2] : null;

		public RenderTexture previousMotionTexture =>
			motionTextures.Length >= 2 ? motionTextures[(currentIdx + 1) % 2] : null;

		[EditorButton("Reload")]
		private void Awake()
		{
			if (!curlNoise || !motionComputeShader)
			{
				return;
			}

			motionTextures = new RenderTexture[]
			{
				new RenderTexture(curlNoise.width, curlNoise.height, 0, RenderTextureFormat.RGFloat),
				new RenderTexture(curlNoise.width, curlNoise.height, 0, RenderTextureFormat.RGFloat),
			};

			motionTextures[0].enableRandomWrite = true;
			motionTextures[1].enableRandomWrite = true;
			motionTextures[0].wrapMode = TextureWrapMode.Repeat;
			motionTextures[1].wrapMode = TextureWrapMode.Repeat;
			motionTextures[0].Create();
			motionTextures[1].Create();

			motionComputeShader.SetVector("TextureSize", new Vector2(curlNoise.width, curlNoise.height));
			motionComputeShader.SetTexture(1, "CurrentMotion", previousMotionTexture);
			motionComputeShader.SetTexture(1, "NextMotion", currentMotionTexture);
			motionComputeShader.Dispatch(1, curlNoise.width / 8, curlNoise.height / 8, 1);
		}

		private void Update()
		{
			if (dynamicUpdate)
			{
				UpdateMotion();
			}
		}

		[EditorButton]
		public void UpdateMotion()
		{
			if (!curlNoise || !motionComputeShader)
			{
				return;
			}

			currentIdx++;
			motionComputeShader.SetFloat("Speed", speed);
			motionComputeShader.SetFloat("DeltaTime", Time.deltaTime);
			motionComputeShader.SetVector("TextureSize", new Vector2(curlNoise.width, curlNoise.height));
			motionComputeShader.SetTexture(0, "CurrentMotion", previousMotionTexture);
			motionComputeShader.SetTexture(0, "NextMotion", currentMotionTexture);
			motionComputeShader.SetTexture(0, "CurlNoise", curlNoise);
			motionComputeShader.Dispatch(0, curlNoise.width / 8, curlNoise.height / 8, 1);
		}

		private void OnGUI()
		{
			if (debug)
			{
				GUI.DrawTexture(new Rect(0, 0, 1024, 1024), currentMotionTexture, ScaleMode.ScaleToFit, false);
			}
		}
	}
}