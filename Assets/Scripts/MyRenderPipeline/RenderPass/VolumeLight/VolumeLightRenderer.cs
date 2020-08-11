using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.VolumeLight
{
	[ExecuteInEditMode]
	[RequireComponent(typeof(Light))]
	public class VolumeLightRenderer : MonoBehaviour
	{
		public Vector2 rayMarchingRange = new Vector2(0, 100);
		public int rayMarchingSteps = 8;
		[Range(0, 1)] public float incomingLoss = 0;
		public float lightDistance = 100;
		public bool extinctionOverride = false;
		public float visibilityDistance = 100;
		public float intensityMultiplier = 1;

		private List<Vector4> planes = new List<Vector4>(6);

		private float previousAngle;
		private float previousRange;
		private LightType previousLightType;

		public Light TheLight { get; private set; }
		public Mesh VolumeMesh { get; private set; }

		private void Awake()
		{
			TheLight = GetComponent<Light>();
			VolumeMesh = new Mesh();
			UpdateMesh();
			previousAngle = TheLight.spotAngle;
			previousRange = TheLight.range;
			previousLightType = TheLight.type;
		}

		private void Update()
		{
			if (TheLight.spotAngle != previousAngle || TheLight.range != previousRange ||
			    TheLight.type != previousLightType )
			{
				previousAngle = TheLight.spotAngle;
				previousRange = TheLight.range;
				previousLightType = TheLight.type;
				UpdateMesh();
			}
		}

		private void UpdateMesh()
		{
			//灯光的椎体 mesh
			if (TheLight.type == LightType.Spot)
			{
				var tanFOV = Mathf.Tan(TheLight.spotAngle / 2 * Mathf.Deg2Rad);
				var verts = new Vector3[]
				{
					new Vector3(0, 0, 0),
					new Vector3(-tanFOV, -tanFOV, 1) * TheLight.range,
					new Vector3(-tanFOV, tanFOV, 1) * TheLight.range,
					new Vector3(tanFOV, tanFOV, 1) * TheLight.range,
					new Vector3(tanFOV, -tanFOV, 1) * TheLight.range,
				};

				VolumeMesh.Clear();
				VolumeMesh.vertices = verts;
				VolumeMesh.triangles = new int[]
				{
					0, 1, 2,
					0, 2, 3,
					0, 3, 4,
					0, 4, 1,
					1, 4, 3,
					1, 3, 2,
				};
				VolumeMesh.RecalculateNormals();
			}
			else if (TheLight.type == LightType.Directional)
			{//平行光还是要有mesh  避免不渲染
				VolumeMesh.Clear();

				VolumeMesh.vertices = new Vector3[]
				{
					new Vector3(-1, -1, -1),
					new Vector3(-1, 1, -1),
					new Vector3(1, 1, -1),
					new Vector3(1, -1, -1),
					new Vector3(-1, -1, 1),
					new Vector3(-1, 1, 1),
					new Vector3(1, 1, 1),
					new Vector3(1, -1, 1),
				};

				VolumeMesh.triangles = new int[]
				{
					0, 1, 2, 0, 2, 3,
					0, 4, 5, 0, 5, 1,
					1, 5, 6, 1, 6, 2,
					2, 6, 7, 2, 7, 3,
					0, 3, 7, 0, 7, 4,
					4, 6, 5, 4, 7, 6,
				};

				VolumeMesh.RecalculateNormals();
			}
			
		}

		public List<Vector4> GetVolumeBoundFaces(Camera camera)
		{
			planes.Clear();
			Matrix4x4 viewProjection = Matrix4x4.identity;
			if (TheLight.type == LightType.Spot)
			{
				//如果是聚光灯  则用聚光灯的VP
				//灯光是反的
				viewProjection = Matrix4x4.Perspective(TheLight.spotAngle, 1, 0.03f, TheLight.range) *
				                 Matrix4x4.Scale(new Vector3(1, 1, -1)) * TheLight.transform.worldToLocalMatrix;
				var m0 = viewProjection.GetRow(0); //X偏移
				var m1 = viewProjection.GetRow(1); //y偏移
				var m2 = viewProjection.GetRow(2); //z偏移
				var m3 = viewProjection.GetRow(3); //中心点

				//shader处理负数
				planes.Add(-(m3 + m0));
				planes.Add(-(m3 - m0));
				planes.Add(-(m3 + m1));
				planes.Add(-(m3 - m1));
				//planes.Add(-(m3 + m2)); //ignore near
				planes.Add(-(m3 - m2));
			}
			else if (TheLight.type == LightType.Directional)
			{
				//如果是平行光 则用 摄像机的VP
				viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
				var m2 = viewProjection.GetRow(2);
				var m3 = viewProjection.GetRow(3);
				//shader处理负数
				planes.Add(-(m3 + m2)); //only near plane
			}

			return planes;
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireMesh(VolumeMesh, 0, transform.position, transform.rotation, transform.localScale);
		}
	}
}