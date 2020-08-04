using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.Utils
{
	public static class RenderUtils
	{
		private static Mesh fullScreenMesh;
		public static Mesh FullScreenMesh
		{
			get
			{
				if (!fullScreenMesh)
					fullScreenMesh = GenerateFullScreenQuad();
				return fullScreenMesh;
			}
		}

		public static Mesh GenerateFullScreenQuad()
		{
			var mesh = new Mesh();
			mesh.Clear();
			mesh.vertices = new Vector3[]
			{
				new Vector3(-1,-1,0),
				new Vector3(1,-1,0),
				new Vector3(1,1,0),
				new Vector3(-1,1,0),
			};
			mesh.triangles = new int[]
			{
				0, 2, 1,
				0, 3, 2
			};
			mesh.uv = new Vector2[]
			{
				new Vector2(0, 0),
				new Vector2(1, 0),
				new Vector2(1, 1),
				new Vector2(0, 1),
			};
			return mesh;
		}
		
		public static void BlitFullScreen(this CommandBuffer cmd, RenderTargetIdentifier src, RenderTargetIdentifier dst, Material mat, int pass)
		{
			cmd.SetGlobalTexture("_MainTex", src);
			cmd.SetRenderTarget(dst);
			cmd.DrawMesh(FullScreenMesh, Matrix4x4.identity, mat, 0, pass);
		}

		public static void SetCameraParams(this CommandBuffer cmd, Camera camera, bool renderToTexture = false)
		{
			var tanFov = Mathf.Tan(camera.fieldOfView / 2 * Mathf.Deg2Rad);
			var tanFovWidth = tanFov * camera.aspect;
			cmd.SetGlobalVector("_DepthParams", new Vector2(tanFovWidth, tanFov));

			cmd.SetGlobalVector("_WorldCameraPos", camera.transform.position);

			cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

			cmd.SetGlobalMatrix("_ViewProjectionInverseMatrix", (camera.projectionMatrix * camera.worldToCameraMatrix).inverse);
			var gpuVP = GL.GetGPUProjectionMatrix(camera.projectionMatrix, renderToTexture) * camera.worldToCameraMatrix;
			cmd.SetGlobalMatrix("_GPUViewProjectionInverseMatrix", gpuVP.inverse);
		}

		public static Matrix4x4 ProjectionToWorldMatrix(Camera camera)
		{
			return (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
		}
	}
}