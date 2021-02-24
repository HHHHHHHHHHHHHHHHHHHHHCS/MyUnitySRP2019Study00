using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

//using static MyRenderPipeline.Other.OtherUtils;

namespace MyRenderPipeline.RenderPass.BachRenderer
{
	//https://docs.unity3d.com/ScriptReference/Rendering.BatchRendererGroup.AddBatch.html
	public class BatchRender : MonoBehaviour
	{
		struct CullData
		{
			public Bounds bound;
			public float3 position;
			public float minDistance;
			public float maxDistance;
		}

		private static readonly int BaseColor_ID = Shader.PropertyToID("_BaseColor");


		[SerializeField] private Mesh mesh;

		[SerializeField] private Mesh lowMesh;

		[SerializeField] private float lodDis;

		[SerializeField] private Material material;

		public bool log = false;

		private BatchRendererGroup batchRendererGroup;
		private NativeArray<CullData> cullData;
		private JobHandle cullingDependency;


		private void Awake()
		{
			batchRendererGroup = new BatchRendererGroup(MyOnPerformCulling);
			cullData = new NativeArray<CullData>(25000, Allocator.Persistent);

			for (int i = 0; i < 50; i++)
			{
				var pos = new float3[50];
				var rot = new quaternion[50];
				var scale = new float3[50];
				for (int j = 0; j < 50; j++)
				{
					pos[j] = new float3(j * 2, 0, i * 2);
					rot[i] = Random.rotation;
					scale[i] = Random.value;
				}

				AddBatch(100 * i, 50, pos, rot, scale);
			}
		}

		private void OnDestroy()
		{
			if (batchRendererGroup != null)
			{
				cullingDependency.Complete();
				batchRendererGroup.Dispose();
				batchRendererGroup = null;
				cullData.Dispose();
			}
		}

		private void AddBatch(int offset, int count, float3[] pos, quaternion[] rot, float3[] scale)
		{
			var localBound = mesh.bounds;
			var block = new MaterialPropertyBlock();
			var colors = new List<Vector4>();

			for (int i = 0; i < count; i++)
			{
				colors.Add(new Vector4(Random.value, Random.value, Random.value, Random.value));
			}

			block.SetVectorArray(BaseColor_ID, colors);


			//下面的Bounds是需要组合过的
			var batchIndex = batchRendererGroup.AddBatch(
				mesh,
				0,
				material,
				0,
				ShadowCastingMode.On,
				true,
				false,
				new Bounds(Vector3.zero, 1000 * Vector3.one),
				count,
				block,
				null
			);

			//TODO:能不能不get 感觉直接给cullData赋值也可以
			var matrices = batchRendererGroup.GetBatchMatrices(batchIndex);
			for (int i = 0; i < count; i++)
			{
				float4x4 tempMatrix = matrices[i] = float4x4.TRS(pos[i], rot[i], scale[i]);
				Bounds aabb = OtherUtils.Transform(tempMatrix, localBound);
				cullData[offset + i] = new CullData()
				{
					bound = aabb,
					position = pos[i],
					minDistance = 0,
					maxDistance = lodDis
				};
			}

			//-----------------
			for (int i = 0; i < count; i++)
			{
				colors[i] = (new Vector4(Random.value, Random.value, Random.value, Random.value));
			}

			block.SetVectorArray(BaseColor_ID, colors);

			//https://docs.unity3d.com/ScriptReference/Rendering.BatchRendererGroup.AddBatch.html
			//下面的Bounds是需要组合过的
			batchIndex = batchRendererGroup.AddBatch(
				mesh,
				0,
				material,
				0,
				ShadowCastingMode.On,
				true,
				false,
				new Bounds(Vector3.zero, 1000 * Vector3.one),
				count,
				block,
				null
			);

			matrices = batchRendererGroup.GetBatchMatrices(batchIndex);
			for (int i = 0; i < count; i++)
			{
				var tempMatrix = matrices[i] = float4x4.TRS(pos[i], rot[i], scale[i]);
				Bounds aabb = OtherUtils.Transform(tempMatrix, localBound);
				cullData[offset + count + i] = new CullData()
				{
					bound = aabb,
					position = pos[i],
					minDistance = lodDis,
					maxDistance = 10000,
				};
			}
		}

		private JobHandle MyOnPerformCulling(BatchRendererGroup renderergroup, BatchCullingContext cullingContext)
		{
			var planes = OtherUtils.BuildSOAPlanePackets(cullingContext.cullingPlanes, Allocator.TempJob);
			var lodParams = OtherUtils.CalculateLODParams(cullingContext.lodParameters);
			var cull = new MyCullJob()
			{
				planes = planes,
				lodParams = lodParams,
				indexList = cullingContext.visibleIndices,
				batches = cullingContext.batchVisibility,
				cullDatas = cullData,
			};
			//TODO: 50 + 50 = 100  但是应该不是50吗
			//TODO:dependency应该没有用
			var handle = cull.Schedule(100, 32, cullingDependency);
			cullingDependency = JobHandle.CombineDependencies(handle, cullingDependency);
			return handle;
		}

		[BurstCompile]
		private struct MyCullJob : IJobParallelFor
		{
			[ReadOnly] public OtherUtils.LODParams lodParams;
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<OtherUtils.PlanePacket4> planes;

			[ReadOnly, NativeDisableParallelForRestriction]
			public NativeArray<CullData> cullDatas;
			
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativeArray<BatchVisibility> batches;
			
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativeArray<int> indexList;

			public void Execute(int index)
			{
				throw new NotImplementedException();
			}
		}
	}
}