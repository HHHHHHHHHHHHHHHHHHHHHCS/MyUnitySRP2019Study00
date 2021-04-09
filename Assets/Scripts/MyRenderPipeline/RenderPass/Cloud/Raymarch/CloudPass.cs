using System.Linq;
using MyRenderPipeline.RenderPass.Common;
using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud.Raymarch
{
	[CreateAssetMenu(fileName = "CloudPass", menuName = "MyRP/RenderPass/CloudPass")]
	public class CloudPass : MyRenderPassAsset
	{
		public bool enablePass = true;
		public bool drawFullScreen = false;
		public Material material;
		public ComputeShader curlNoiseMotionComputeShader;
		public RenderTexture curlNoiseTexture;

		public override MyRenderPass CreateRenderPass()
		{
			return new CloudPassRenderer(this);
		}
	}

	public class CloudPassRenderer : MyRenderPassRenderer<CloudPass>
	{
		private const string k_profilingTag = "Volumetric Cloud Rendering";
		private readonly ProfilingSampler profilingSampler = new ProfilingSampler(k_profilingTag);

		private Mesh screenMesh;
		private CurlNoiseMotionRenderer curlNoiseMotionRenderer;


		public CloudPassRenderer(CloudPass asset) : base(asset)
		{
			screenMesh = RenderUtils.GenerateFullScreenQuad();
		}

		public override void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			//基本不运行
			if (curlNoiseMotionRenderer == null && asset.curlNoiseMotionComputeShader && asset.curlNoiseTexture)
			{
				curlNoiseMotionRenderer = new CurlNoiseMotionRenderer(asset.curlNoiseTexture,
					asset.curlNoiseMotionComputeShader,
					new Vector3Int(asset.curlNoiseTexture.width, asset.curlNoiseTexture.height,
						asset.curlNoiseTexture.volumeDepth));
			}

			SetupLights(context, ref renderingData);
		}

		public override void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			if (!asset.enablePass)
			{
				return;
			}


			if (renderingData.camera.cameraType == CameraType.Preview)
			{
				return;
			}

			if (!asset.material || !screenMesh)
			{
				return;
			}

			var cmd = CommandBufferPool.Get(k_profilingTag);

			using (new ProfilingScope(cmd, profilingSampler))
			{
				if (curlNoiseMotionRenderer != null)
				{
					//不运行
					var buffer = curlNoiseMotionRenderer.Update(cmd);
					cmd.SetGlobalBuffer("_MotionPosBuffer", buffer);
					var size = curlNoiseMotionRenderer.size;
					cmd.SetGlobalVector("_MotionPosBufferSize", new Vector3(size.x, size.y, size.z));
				}

				cmd.SetGlobalVector("_WorldCameraPos", renderingData.camera.transform.position);
				cmd.SetGlobalVector("_CameraClipPlane",
					new Vector3(renderingData.camera.nearClipPlane, renderingData.camera.farClipPlane,
						renderingData.camera.farClipPlane - renderingData.camera.nearClipPlane));
				cmd.SetGlobalMatrix("_ViewProjectionInverseMatrix",
					RenderUtils.ProjectionToWorldMatrix(renderingData.camera));

				//Resources.FindObjectsOfTypeAll 在内存中的资源
				var curlNoiseMotion = Resources.FindObjectsOfTypeAll<CurlNoiseMotion2D>().FirstOrDefault();
				if (curlNoiseMotion)
				{
					cmd.SetGlobalTexture("_CurlNoiseMotionTex", curlNoiseMotion.currentMotionTexture);
				}

				if (asset.drawFullScreen)
				{
					cmd.DrawMesh(screenMesh, RenderUtils.ProjectionToWorldMatrix(renderingData.camera), asset.material,
						0,
						0);
				}
				else
				{
					//美术效果不是很好  而且有问题算了
					var cubes = Object.FindObjectsOfType<VolumetricCloudCube>();
					foreach (var cube in cubes)
					{
						cmd.SetGlobalVector("_CubeSize", cube.transform.localScale);
						cmd.SetGlobalVector("_CubePos", cube.transform.position);
						cmd.DrawRenderer(cube.GetComponent<MeshRenderer>(), asset.material, 0, 1);
					}
				}


			}
			
			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}


		private void SetupLights(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
			var cmd = CommandBufferPool.Get();

			renderingData.lights = renderingData.cullResults.visibleLights;
			var mainLightIdx = GetMainLightIndex(ref renderingData);

			if (mainLightIdx >= 0)
			{
				var mainLight = renderingData.lights[GetMainLightIndex(ref renderingData)];

				cmd.SetGlobalColor("_MainLightColor", mainLight.finalColor);
				cmd.SetGlobalVector("_MainLightDirection", mainLight.light.transform.forward);
			}
			else
			{
				cmd.SetGlobalColor("_MainLightColor", Color.black);
				cmd.SetGlobalVector("_MainLightPosition", Vector4.zero);
			}

			cmd.SetGlobalColor("_AmbientSkyColor", RenderSettings.ambientSkyColor);

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();
			CommandBufferPool.Release(cmd);
		}

		private int GetMainLightIndex(ref MyRenderingData renderingData)
		{
			var lights = renderingData.cullResults.visibleLights;
			var sun = RenderSettings.sun;
			if (sun == null)
				return -1;
			for (var i = 0; i < lights.Length; i++)
			{
				if (lights[i].light == sun)
					return i;
			}

			return -1;
		}
	}
}