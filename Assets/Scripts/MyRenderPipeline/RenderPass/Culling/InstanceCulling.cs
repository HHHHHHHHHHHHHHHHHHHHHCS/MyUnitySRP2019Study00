using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Culling
{
	//新的C# 可以使用in 关键字 提升 struct 性能
	public class InstanceCulling : MonoBehaviour
	{
		public enum RendererType
		{
			NoCulling,
			CPUCulling,
			ThreadCulling,
			JobCulling,
			GPUCulling,
		}

		public enum IntersectResult
		{
			Outside = 0,
			Inside = 1,
			Intersect = 2,
		}

		public struct ObjInfo
		{
			public Bounds bounds;
			public Quaternion rotation;
			public Vector3 scale;
		}

		private static readonly int MatrixsIDBuffer = Shader.PropertyToID("_MatrixsBuffer");

		public Mesh mesh;
		public Material mat;

		public RendererType rendererType;

		public int x = 50;
		public int y = 50;
		public int z = 50;


		private int count;
		private List<ObjInfo> objInfos;
		private List<Matrix4x4> result;

		private ComputeBuffer bufferCB;
		private ComputeBuffer argsBufferCB;
		private uint[] args;


		private void Start()
		{
			Camera.main.transform.position = new Vector3(x / 2.0f, y / 2.0f, z / 2.0f);

			count = x * y * z;
			objInfos = new List<ObjInfo>(count);
			result = new List<Matrix4x4>(count / 10);

			int index = 0;
			for (int i = 0; i < z; i++)
			{
				for (int j = 0; j < y; j++)
				{
					for (int k = 0; k < z; k++)
					{
						objInfos.Add(new ObjInfo()
						{
							//如果有旋转和缩放aabb 应该另算
							bounds = new Bounds(new Vector3(k, j, i), Vector3.one),
							rotation = quaternion.identity,
							scale = Vector3.one * 0.5f
						});
					}
				}
			}

			argsBufferCB = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
			args = new[]
				{mesh.GetIndexCount(0), (uint) 0, mesh.GetIndexStart(0), mesh.GetBaseVertex(0), 0u};
		}

		private void Update()
		{
			var planes = new NativeArray<Plane>(GeometryUtility.CalculateFrustumPlanes(Camera.main), Allocator.TempJob);

			result.Clear();

			switch (rendererType)
			{
				case RendererType.NoCulling:
					NoCulling(objInfos, result);
					break;
				case RendererType.CPUCulling:
					CPUCulling(planes, objInfos, result);
					break;
				case RendererType.ThreadCulling:
					MultiThreadCulling(planes, objInfos, result);
					break;
				case RendererType.JobCulling:
					JobCulling(planes, objInfos, result);
					break;
			}

			planes.Dispose();

			if (bufferCB == null || bufferCB.count != result.Count)
			{
				bufferCB?.Dispose();
				bufferCB = new ComputeBuffer(result.Count, sizeof(float) * 16, ComputeBufferType.Structured);
			}

			bufferCB.SetData(result);
			mat.SetBuffer(MatrixsIDBuffer, bufferCB);

			if (args[1] != result.Count)
			{
				args[1] = (uint) result.Count;
			}

			argsBufferCB.SetData(args);

			var objBounds = new Bounds(new Vector3(x / 2.0f, y / 2.0f, z / 2.0f), new Vector3(x, y, z));
			Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, objBounds, argsBufferCB);
		}

		[BurstCompile]
		public static Matrix4x4 ObjInfoToMatrix(ObjInfo info) =>
			Matrix4x4.TRS(info.bounds.center, info.rotation, info.scale);

		//也可以通过下面API调用  只是比较慢
		//GeometryUtility.CalculateFrustumPlanes(cam);
		//GeometryUtility.TestPlanesAABB(planes, objCollider.bounds)
		[BurstCompile]
		public static IntersectResult TestPlaneAABBFast(NativeArray<Plane> planes, Bounds bounds
			, bool testIntersection = false)
		{
			Vector3 boundMin = bounds.min;
			Vector3 boundMax = bounds.max;
			Vector3 vmin, vmax;
			var testResult = IntersectResult.Inside;

			for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
			{
				var normal = planes[planeIndex].normal;
				var planeDistance = planes[planeIndex].distance;

				if (normal.x < 0)
				{
					vmin.x = boundMin.x;
					vmax.x = boundMax.x;
				}
				else
				{
					vmin.x = boundMax.x;
					vmax.x = boundMin.x;
				}

				if (normal.y < 0)
				{
					vmin.y = boundMin.y;
					vmax.y = boundMax.y;
				}
				else
				{
					vmin.y = boundMax.y;
					vmax.y = boundMin.y;
				}

				if (normal.z < 0)
				{
					vmin.z = boundMin.z;
					vmax.z = boundMax.z;
				}
				else
				{
					vmin.z = boundMax.z;
					vmax.z = boundMin.z;
				}

				var dot1 = normal.x * vmin.x + normal.y * vmin.y + normal.z * vmin.z;
				if (dot1 + planeDistance < 0)
				{
					return IntersectResult.Outside;
				}

				if (testIntersection)
				{
					//外侧面相交
					var dot2 = normal.x * vmax.x + normal.y * vmax.y + normal.z * vmax.z;
					if (dot2 + planeDistance < 0)
					{
						testResult = IntersectResult.Intersect;
					}
				}
			}

			return testResult;
		}

		public void NoCulling(List<ObjInfo> infos, List<Matrix4x4> result)
		{
			foreach (var item in infos)
			{
				result.Add(ObjInfoToMatrix(item));
			}
		}

		public void CPUCulling(NativeArray<Plane> planes, List<ObjInfo> infos, List<Matrix4x4> result)
		{
			foreach (var item in infos)
			{
				var noCulled = TestPlaneAABBFast(planes, item.bounds);
				if (noCulled != IntersectResult.Outside)
				{
					result.Add(ObjInfoToMatrix(item));
				}
			}
		}

		public void MultiThreadCulling(NativeArray<Plane> planes, List<ObjInfo> infos,
			List<Matrix4x4> result)
		{
			ConcurrentQueue<Matrix4x4> queue = new ConcurrentQueue<Matrix4x4>();
			var threadCount = 32;
			var tasks = new Task[threadCount];
			var idx = -1;

			var visibled = -1;
			for (var i = 0; i < threadCount; i++)
			{
				tasks[i] = Task.Factory.StartNew(ThreadCulling);
			}

			Task.WaitAll(tasks);
			result.AddRange(queue.ToArray());

			void ThreadCulling()
			{
				while (true)
				{
					var tmp = Interlocked.Increment(ref idx);

					if (tmp >= infos.Count)
					{
						break;
					}

					var item = objInfos[tmp];

					var isIn = TestPlaneAABBFast(planes, item.bounds);
					if (isIn != IntersectResult.Outside)
					{
						queue.Enqueue(ObjInfoToMatrix(item));
					}
				}
			}
		}

		public void JobCulling(NativeArray<Plane> planes, List<ObjInfo> infos,
			List<Matrix4x4> result)
		{
			NativeArray<ObjInfo> objinfos = new NativeArray<ObjInfo>(infos.ToArray(), Allocator.TempJob);

			var cullingJob = new CullingJob()
			{
				objInfos = objinfos,
				planes = planes
			};


			NativeList<int> indexList = new NativeList<int>(Allocator.TempJob);
			//第三个参数innerloopBatchCount  表示分块大小
			var handle = cullingJob.ScheduleAppend(indexList, objinfos.Length, 64);
			handle.Complete();

			NativeArray<Matrix4x4> matrix4X4s = new NativeArray<Matrix4x4>(indexList.Length, Allocator.TempJob);
			var makeJob = new MakeMatrixListJob()
			{
				objInfos = objinfos,
				indexList = indexList,
				matrix4X4s = matrix4X4s
			};
			handle = makeJob.Schedule(indexList.Length, 64);
			handle.Complete();
			result.AddRange(matrix4X4s);

			objinfos.Dispose();
			indexList.Dispose();
			matrix4X4s.Dispose();
		}

		[BurstCompile]
		public struct CullingJob : IJobParallelForFilter
		{
			[ReadOnly] public NativeArray<ObjInfo> objInfos;

			[ReadOnly] public NativeArray<Plane> planes;


			public bool Execute(int index)
			{
				var isIn = TestPlaneAABBFast(planes, objInfos[index].bounds);
				return isIn != IntersectResult.Outside;
			}
		}

		[BurstCompile]
		public struct MakeMatrixListJob : IJobParallelFor
		{
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativeArray<ObjInfo> objInfos;

			[ReadOnly, NativeDisableParallelForRestriction]
			public NativeArray<int> indexList;

			public NativeArray<Matrix4x4> matrix4X4s;

			public void Execute(int index)
			{
				matrix4X4s[index] = ObjInfoToMatrix(objInfos[indexList[index]]);
			}
		}
	}
}