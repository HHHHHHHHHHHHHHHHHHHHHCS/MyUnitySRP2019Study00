using System;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud
{
	public class MeshCloud : MonoBehaviour
	{
		private static readonly int Thickness_ID = Shader.PropertyToID("_Thickness");
		private static readonly int ClipRate_ID = Shader.PropertyToID("_ClipRate");


		[Range(1,100)]
		public int count = 20;
		[Range(0.01f,10.0f)]
		public float cloudThickness = 1f;
		public Mesh mesh;
		public Material cloudMaterial;
		public int layer;
		public bool receiveShadows;
		public bool useGPUInstancing;

		private Camera camera;
		private float offset;
		private float[] offsets;
		private float[] clipRates;

		private Matrix4x4 matrix;
		private Matrix4x4[] matrices;

		private MaterialPropertyBlock property;

		private void Awake()
		{
			camera = Camera.main;
			property = new MaterialPropertyBlock();
		}

		private void Update()
		{
			offset = cloudThickness / count;

			if (useGPUInstancing)
			{
				if (matrices == null || matrices.Length != count)
				{
					matrices = new Matrix4x4[count];
					offsets = new float[count];
					clipRates = new float[count];
				}
			}

			for (int n = 0; n < count; n++)
			{
				matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
				float thickness = offset * n;
				float clipRate = (float) n / count;

				if (useGPUInstancing)
				{
					matrices[n] = matrix;
					offsets[n] = thickness;
					clipRates[n] = clipRate;
				}
				else
				{
					property.SetFloat(Thickness_ID, thickness);
					property.SetFloat(ClipRate_ID, thickness);
					Graphics.DrawMesh(mesh, matrix, cloudMaterial, layer, camera, 0, property, false, receiveShadows,
						false);
				}
			}
			
			if (useGPUInstancing)
			{
				property.SetFloatArray(Thickness_ID, offsets);
				property.SetFloatArray(ClipRate_ID, clipRates);

				Graphics.DrawMeshInstanced(mesh, 0, cloudMaterial, matrices, count, property,
					UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows, layer, camera);
			}
		}
	}
}