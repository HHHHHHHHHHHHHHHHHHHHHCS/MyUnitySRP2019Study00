using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace MyRenderPipeline.RenderPass.GrassCulling
{
	[ExecuteAlways]
	public class GrassCullingRenderer : MonoBehaviour
	{
		private const float c_cellSizeX = 10; //unity uint (m)
		private const float c_cellSizeZ = 10; //unity uint (m)

		public static GrassCullingRenderer instance;

		[Header("Setting"), Tooltip("这个设定对性能影响很大")]
		public float drawDistance = 125;

		public Material instanceMaterial;

		[Header("Internal")] public ComputeShader cullingComputeShader;

		//外部传入位置
		[NonSerialized] public List<Vector3> allGrassPos = new List<Vector3>();

		private Camera cam;

		private int cellCountX = -1;
		private int cellCountZ = -1;
		private int dispatchCount = -1;

		private int instanceCountCache = -1;
		private Mesh cachedGrassMesh;

		private ComputeBuffer allInstancesPosWSBuffer;
		private ComputeBuffer visibleInstancesOnlyPosWSIDBuffer;
		private ComputeBuffer argsBuffer;

		private List<Vector3>[] cellPosWSsList;
		private float minX, minZ, maxX, maxZ;
		private List<int> visibleCellIDList = new List<int>();
		private Plane[] cameraFrustumPlanes = new Plane[6];

		private bool shouldBatchDispatch = true;

		private void OnEnable()
		{
			instance = this;
			cam = Camera.main;
		}

		private void LateUpdate()
		{
			if (!UpdateAllInstanceTransformBufferIfNeeded())
			{
				return;
			}

			visibleCellIDList.Clear();

			//https://docs.unity3d.com/ScriptReference/GeometryUtility.CalculateFrustumPlanes.html
			float cameraOriginalFarPlane = cam.farClipPlane;
			cam.farClipPlane = drawDistance;
			//自己写了一个类似于unity 的  CalculateFrustumPlanes()  但是还是用unity 的CPP 代码
			//CalculateFrustumPlanes(cam, cameraFrustumPlanes);
			GeometryUtility.CalculateFrustumPlanes(cam,
				cameraFrustumPlanes); //Ordering: [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
			cam.farClipPlane = cameraOriginalFarPlane;


			Profiler.BeginSample("CPU cell frustum culling (heavy)");
			//有几个草块可以看见
			//TODO:(A)用四叉树测试替换这个forloop?
			//TODO:(B)把这个forloop转换成job+burst?(UnityException:TestPlaneSabb只能从主线程调用。)
			Vector3 sizeWS = new Vector3(Mathf.Abs(maxX - minX) / cellCountX, 0,
				Mathf.Abs(maxZ - minZ) / cellCountZ);
			for (int i = 0; i < cellPosWSsList.Length; i++)
			{
				Vector3 centerPosWS = new Vector3(i % cellCountX + 0.5f, 0, i / cellCountX + 0.5f);
				centerPosWS.x = Mathf.Lerp(minX, maxX, centerPosWS.x / cellCountX);
				centerPosWS.z = Mathf.Lerp(minZ, maxZ, centerPosWS.z / cellCountZ);
				Bounds cellBound = new Bounds(centerPosWS, sizeWS);

				//https://docs.unity3d.com/ScriptReference/GeometryUtility.TestPlanesAABB.html
				if (GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, cellBound))
				{
					visibleCellIDList.Add(i);
				}
			}

			Profiler.EndSample();


			Matrix4x4 v = cam.worldToCameraMatrix;
			Matrix4x4 p = cam.projectionMatrix;
			Matrix4x4 vp = p * v;

			//设置计数器为0
			visibleInstancesOnlyPosWSIDBuffer.SetCounterValue(0);

			cullingComputeShader.SetMatrix("_VPMatrix", vp);
			cullingComputeShader.SetFloat("_MaxDrawDistance", drawDistance);

			dispatchCount = 0;
			for (int i = 0; i < visibleCellIDList.Count; i++)
			{
				int targetCellFlattenID = visibleCellIDList[i];
				int memoryOffset = 0;
				for (int j = 0; j < targetCellFlattenID; j++)
				{
					//添加草块里面的草的数量
					memoryOffset += cellPosWSsList[j].Count;
				}

				cullingComputeShader.SetInt("_StartOffset", memoryOffset); //剔除从偏移位置开始的读取数据，将从单元在内存中的总偏移量开始

				int jobLength = cellPosWSsList[targetCellFlattenID].Count;

				//合并n个dispatchs 为一个 dispatch , 如果内存是连续的在 allInstancesPosWSBuffer
				if (shouldBatchDispatch)
				{
					while ((i < visibleCellIDList.Count - 1)
					       && (visibleCellIDList[i + 1] == visibleCellIDList[i] + 1))
					{
						jobLength += cellPosWSsList[visibleCellIDList[i + 1]].Count;
						i++;
					}
				}

				cullingComputeShader.Dispatch(0, Mathf.CeilToInt(jobLength / 64f), 1, 1);
				dispatchCount++;
			}

			//复制计数器value  args[1] = dstOffsetBytes = sizeof(uint) / sizeof(byte) = 4
			ComputeBuffer.CopyCount(visibleInstancesOnlyPosWSIDBuffer, argsBuffer, 4);

			Bounds renderBound = new Bounds();
			renderBound.SetMinMax(new Vector3(minX, 0, minZ), new Vector3(maxX, 0, maxZ));
			Graphics.DrawMeshInstancedIndirect(GetGrassMeshCache(), 0, instanceMaterial, renderBound, argsBuffer);
		}


		private void OnGUI()
		{
			GUI.contentColor = Color.black;
			GUI.Label(new Rect(200, 0, 400, 60),
				$"After CPU cell frustum culling,\n" +
				$"-Visible cell count = {visibleCellIDList.Count}/{cellCountX * cellCountZ}\n" +
				$"-Real compute dispatch count = {dispatchCount} (saved by batching = {visibleCellIDList.Count - dispatchCount})");

			shouldBatchDispatch = GUI.Toggle(new Rect(400, 400, 200, 100), shouldBatchDispatch, "shouldBatchDispatch");
		}

		private void OnDisable()
		{
			allInstancesPosWSBuffer?.Release();
			allInstancesPosWSBuffer = null;

			visibleInstancesOnlyPosWSIDBuffer?.Release();
			visibleInstancesOnlyPosWSIDBuffer = null;

			argsBuffer?.Release();
			argsBuffer = null;

			instance = null;
		}

		private Mesh GetGrassMeshCache()
		{
			if (!cachedGrassMesh)
			{
				cachedGrassMesh = new Mesh();

				Vector3[] verts = new Vector3[3];
				verts[0] = new Vector3(-0.25f, 0f);
				verts[1] = new Vector3(+0.25f, 0f);
				verts[2] = new Vector3(-0.0f, 1f);

				int[] triangles = new int[3] {2, 1, 0};

				cachedGrassMesh.SetVertices(verts);
				cachedGrassMesh.SetTriangles(triangles, 0);
			}

			return cachedGrassMesh;
		}

		private bool UpdateAllInstanceTransformBufferIfNeeded()
		{
			if (instanceMaterial == null || allGrassPos.Count == 0)
			{
				return false;
			}

			instanceMaterial.SetVector("_PivotPosWS", transform.position);
			instanceMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));

			if (instanceCountCache == allGrassPos.Count &&
			    argsBuffer != null &&
			    allInstancesPosWSBuffer != null &&
			    visibleInstancesOnlyPosWSIDBuffer != null)
			{
				return true;
			}


			Debug.Log("UpdateAllInstanceTransformBuffer (Slow)");

			allInstancesPosWSBuffer?.Release();
			allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3);

			visibleInstancesOnlyPosWSIDBuffer?.Release();
			//https://docs.unity3d.com/ScriptReference/ComputeBufferType.html
			//如果不是 Append/Consume/Counter  不能使用ComputeBuffer.CopyCount   SetCounterValue方法
			//而且computeShader 中可以使用 AppendStructuredBuffer.Append
			visibleInstancesOnlyPosWSIDBuffer =
				new ComputeBuffer(allGrassPos.Count, sizeof(uint), ComputeBufferType.Append);

			minX = float.MaxValue;
			minZ = float.MaxValue;
			maxX = float.MinValue;
			maxZ = float.MinValue;

			for (int i = 0; i < allGrassPos.Count; i++)
			{
				Vector3 target = allGrassPos[i];
				minX = Mathf.Min(target.x, minX);
				minZ = Mathf.Min(target.z, minZ);
				maxX = Mathf.Max(target.x, maxX);
				maxZ = Mathf.Max(target.z, maxZ);
			}

			cellCountX = Mathf.CeilToInt((maxX - minX) / c_cellSizeX);
			cellCountZ = Mathf.CeilToInt((maxZ - minZ) / c_cellSizeZ);

			cellPosWSsList = new List<Vector3>[cellCountX * cellCountZ];
			for (int i = 0; i < cellPosWSsList.Length; i++)
			{
				cellPosWSsList[i] = new List<Vector3>();
			}

			for (int i = 0; i < allGrassPos.Count; i++)
			{
				Vector3 pos = allGrassPos[i];

				//find cellID   InverseLerp让值到[0,1]
				int xID = Mathf.Min(cellCountX - 1,
					Mathf.FloorToInt(Mathf.InverseLerp(minX, maxX, pos.x) * cellCountX));
				int zID = Mathf.Min(cellCountZ - 1,
					Mathf.FloorToInt(Mathf.InverseLerp(minZ, maxZ, pos.z) * cellCountZ));

				cellPosWSsList[xID + zID * cellCountX].Add(pos);
			}

			int offset = 0;
			Vector3[] allGrassPosWSSortedByCell = new Vector3[allGrassPos.Count];
			for (int i = 0; i < cellPosWSsList.Length; i++)
			{
				for (int j = 0; j < cellPosWSsList[i].Count; j++)
				{
					allGrassPosWSSortedByCell[offset] = cellPosWSsList[i][j];
					offset++;
				}
			}
			allInstancesPosWSBuffer.SetData(allGrassPosWSSortedByCell);
			
			instanceMaterial.SetBuffer("_AllInstancesTransformBuffer", allInstancesPosWSBuffer);
			instanceMaterial.SetBuffer("_VisibleInstanceOnlyTransformIDBuffer", visibleInstancesOnlyPosWSIDBuffer);

			argsBuffer?.Release();
			uint[] args = new uint[5] {0, 0, 0, 0, 0};
			argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

			var mesh = GetGrassMeshCache();
			args[0] = (uint) mesh.GetIndexCount(0);
			args[1] = (uint) allGrassPos.Count;
			args[2] = (uint) mesh.GetIndexStart(0);
			args[3] = (uint) mesh.GetBaseVertex(0);
			args[4] = 0; //开始实例的偏移

			argsBuffer.SetData(args);

			instanceCountCache = allGrassPos.Count;

			cullingComputeShader.SetBuffer(0, "_AllInstancesPosWSBuffer", allInstancesPosWSBuffer); //in
			cullingComputeShader.SetBuffer(0, "_VisibleInstancesOnlyPosWSIDBuffer",
				visibleInstancesOnlyPosWSIDBuffer); //out

			return true;
		}


		private void CalculateFrustumPlanes(Camera camera, Plane[] planes)
		{
			if (planes == null)
			{
				planes = new Plane[6];
			}
			else if (planes.Length != 6)
			{
				Debug.LogError("Planes array must be of length 6.");
				return;
			}


			var mvp = camera.projectionMatrix * cam.worldToCameraMatrix;

			float[] tmpVec = new float[4];
			float[] otherVec = new float[4];

			Vector3 normal;
			float distance;

			tmpVec[0] = mvp[3]; //[3,0]
			tmpVec[1] = mvp[7]; //[3,1]
			tmpVec[2] = mvp[11]; //[3,2]
			tmpVec[3] = mvp[15]; //[3,3]


			otherVec[0] = mvp[0]; //[0,0]
			otherVec[1] = mvp[4]; //[0,1]
			otherVec[2] = mvp[8]; //[0,2]
			otherVec[3] = mvp[12]; //[0,3]

			//left right
			normal = new Vector3(otherVec[0] + tmpVec[0], otherVec[1] + tmpVec[1], otherVec[2] + tmpVec[2]);
			distance = otherVec[3] + tmpVec[3];
			NormalizedUnsafe(ref normal, ref distance);
			planes[0].normal = normal;
			planes[0].distance = distance;

			normal = new Vector3(-otherVec[0] + tmpVec[0], -otherVec[1] + tmpVec[1], -otherVec[2] + tmpVec[2]);
			distance = -otherVec[3] + tmpVec[3];
			NormalizedUnsafe(ref normal, ref distance);
			planes[1].normal = normal;
			planes[1].distance = distance;


			otherVec[0] = mvp[1]; //[1,0]
			otherVec[1] = mvp[5]; //[1,1]
			otherVec[2] = mvp[9]; //[1,2]
			otherVec[3] = mvp[13]; //[1,3]

			//bottom top
			normal = new Vector3(otherVec[0] + tmpVec[0], otherVec[1] + tmpVec[1], otherVec[2] + tmpVec[2]);
			distance = otherVec[3] + tmpVec[3];
			NormalizedUnsafe(ref normal, ref distance);
			planes[2].normal = normal;
			planes[2].distance = distance;

			normal = new Vector3(-otherVec[0] + tmpVec[0], -otherVec[1] + tmpVec[1], -otherVec[2] + tmpVec[2]);
			distance = -otherVec[3] + tmpVec[3];
			NormalizedUnsafe(ref normal, ref distance);
			planes[3].normal = normal;
			planes[3].distance = distance;


			otherVec[0] = mvp[2]; //[2,0]
			otherVec[1] = mvp[6]; //[2,1]
			otherVec[2] = mvp[10]; //[2,2]
			otherVec[3] = mvp[14]; //[2,3]

			//near far
			normal = new Vector3(otherVec[0] + tmpVec[0], otherVec[1] + tmpVec[1], otherVec[2] + tmpVec[2]);
			distance = otherVec[3] + tmpVec[3];
			NormalizedUnsafe(ref normal, ref distance);
			planes[4].normal = normal;
			planes[4].distance = distance;

			normal = new Vector3(-otherVec[0] + tmpVec[0], -otherVec[1] + tmpVec[1], -otherVec[2] + tmpVec[2]);
			distance = -otherVec[3] + tmpVec[3];
			NormalizedUnsafe(ref normal, ref distance);
			planes[5].normal = normal;
			planes[5].distance = distance;
		}


		private void NormalizedUnsafe(ref Vector3 normal, ref float distance)
		{
			float invMag = 1.0f / normal.magnitude;
			normal *= invMag;
			distance *= invMag;
		}
	}
}