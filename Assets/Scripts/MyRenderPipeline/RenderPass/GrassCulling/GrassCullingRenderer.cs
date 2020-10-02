using System;
using System.Collections.Generic;
using UnityEngine;

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

		private int cellCountX = -1;

		private int cellCountZ = -1;

		private int dispatchCount = -1;


		private int instanceCountCache = -1;
		private Mesh cachedGrassMesh;

		private ComputeBuffer allInstancesPosWSBuffer;

		private ComputeBuffer visibleInstanceOnlyPosWSIDBuffer;

		private ComputeBuffer argsBuffer;

		private List<Vector3>[] cellPosWSsList;

		private float minX, minZ, maxX, maxZ;

		private List<int> visibleCellIdList = new List<int>();

		private Plane[] cameraFrustumPlanes = new Plane[6];

		private bool shouldBatchDispatch = true;

		private void OnEnable()
		{
			instance = this;
		}

		private void LateUpdate()
		{
			UpdateAllInstanceTransformBufferIfNeeded();
		}

		private void OnDisable()
		{
			allInstancesPosWSBuffer?.Release();
			allInstancesPosWSBuffer = null;

			visibleInstanceOnlyPosWSIDBuffer?.Release();
			visibleInstanceOnlyPosWSIDBuffer = null;

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

		private void UpdateAllInstanceTransformBufferIfNeeded()
		{
			instanceMaterial.SetVector("_PivotPosWS", transform.position);
			instanceMaterial.SetVector("_BoundSize", new Vector2(transform.localScale.x, transform.localScale.z));

			if (instanceCountCache == allGrassPos.Count &&
			    argsBuffer != null &&
			    allInstancesPosWSBuffer != null &&
			    visibleInstanceOnlyPosWSIDBuffer != null)
			{
				return;
			}

			Debug.Log("UpdateAllInstanceTransformBuffer (Slow)");

			allInstancesPosWSBuffer?.Release();
			allInstancesPosWSBuffer = new ComputeBuffer(allGrassPos.Count, sizeof(float) * 3);

			visibleInstanceOnlyPosWSIDBuffer?.Release();
			//https://docs.unity3d.com/ScriptReference/ComputeBufferType.html
			//不是 Append/Consume/Counter  不能使用ComputeBuffer.CopyCount方法
			//而且computeShader 中可以使用 AppendStructuredBuffer.Append
			visibleInstanceOnlyPosWSIDBuffer =
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

				//find cellID
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
			instanceMaterial.SetBuffer("_VisibleInstanceOnlyTrasnformIDBuffer", visibleInstanceOnlyPosWSIDBuffer);

			argsBuffer?.Release();
			uint[] args = new uint[5] {0, 0, 0, 0, 0};
			argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

			var mesh = GetGrassMeshCache();
			args[0] = (uint) mesh.GetIndexCount(0);
			args[1] = (uint) allGrassPos.Count;
			args[2] = (uint) mesh.GetIndexStart(0);
			args[3] = (uint) mesh.GetBaseVertex(0);
			args[4] = 0;

			argsBuffer.SetData(args);

			instanceCountCache = allGrassPos.Count;

			cullingComputeShader.SetBuffer(0, "_AllInstancesPosWSBuffer", allInstancesPosWSBuffer); //in
			cullingComputeShader.SetBuffer(0, "_VisibleInstancesOnlyPosWSIDBuffer",
				visibleInstanceOnlyPosWSIDBuffer); //out
		}
	}
}