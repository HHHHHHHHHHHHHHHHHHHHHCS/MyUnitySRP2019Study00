using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

//using static MyRenderPipeline.Other.OtherUtils;

namespace MyRenderPipeline.RenderPass.BatchRenderer
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

		private BatchRendererGroup batchRendererGroup;
		private NativeArray<CullData> cullData;

		//作用是 形成队列关系
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
					rot[j] = quaternion.identity; //Random.rotation;
					scale[j] = 1; //Random.value;
				}

				AddBatch(100 * i, 50, pos, rot, scale);
			}
		}

		// private void Start()
		// {
		// 	// instance 的buffer 需要关闭 batcher
		// 	// ((UniversalRenderPipelineAsset) GraphicsSettings.currentRenderPipeline).useSRPBatcher = false;
		// }

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
			var colors = new List<Vector4>(count);

			for (int i = 0; i < count; i++)
			{
				colors.Add(new Vector4(Random.value, Random.value, Random.value, Random.value));
			}

			// 因为srp batcher buffer的关系 instance buffer 会混乱
			// block.SetVectorArray(BaseColor_ID, colors);


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

			var matrices = batchRendererGroup.GetBatchMatrices(batchIndex);
			for (int i = 0; i < count; i++)
			{
				float4x4 tempMatrix = float4x4.TRS(pos[i], rot[i], scale[i]);
				Bounds aabb = OtherUtils.Transform(tempMatrix, localBound);
				tempMatrix.c0.w = colors[i].x;
				tempMatrix.c1.w = colors[i].y;
				tempMatrix.c2.w = colors[i].z;
				tempMatrix.c3.w = colors[i].w;
				matrices[i] = tempMatrix;
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

			// 因为srp batcher buffer的关系 instance buffer 会混乱
			// block.SetVectorArray(BaseColor_ID, colors);

			//https://docs.unity3d.com/ScriptReference/Rendering.BatchRendererGroup.AddBatch.html
			//下面的Bounds是需要组合过的
			batchIndex = batchRendererGroup.AddBatch(
				lowMesh,
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
				var tempMatrix = float4x4.TRS(pos[i], rot[i], scale[i]);
				Bounds aabb = OtherUtils.Transform(tempMatrix, localBound);
				tempMatrix.c0.w = colors[i].x;
				tempMatrix.c1.w = colors[i].y;
				tempMatrix.c2.w = colors[i].z;
				tempMatrix.c3.w = colors[i].w;
				matrices[i] = tempMatrix;
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
			//传入visibleIndices和batchVisibility 在job里面修改
			var cull = new MyCullJob()
			{
				planes = planes,
				lodParams = lodParams,
				indexList = cullingContext.visibleIndices,
				batches = cullingContext.batchVisibility,
				cullDatas = cullData,
			};
			//50组lod0 + 50组lod1 = 100  
			// cullingDependency 形成队列关系
			var handle = cull.Schedule(100, 32, cullingDependency);
			cullingDependency = JobHandle.CombineDependencies(handle, cullingDependency);
			return handle;
		}

		[BurstCompile]
		private struct MyCullJob : IJobParallelFor
		{
			[ReadOnly] public OtherUtils.LODParams lodParams;

			//完成会自动释放
			[ReadOnly, DeallocateOnJobCompletion] public NativeArray<OtherUtils.PlanePacket4> planes;

			//NativeDisableParallelForRestriction  可以多个线程访问
			[ReadOnly, NativeDisableParallelForRestriction]
			public NativeArray<CullData> cullDatas;

			[NativeDisableParallelForRestriction] public NativeArray<BatchVisibility> batches;

			[NativeDisableParallelForRestriction] public NativeArray<int> indexList;

			public void Execute(int index)
			{
				var bv = batches[index];
				var visibleInstanceIndex = 0;
				var isOrtho = lodParams.isOrtho;
				var distanceScale = lodParams.distanceScale;

				for (int i = 0; i < bv.instancesCount; i++)
				{
					var cullData = cullDatas[index * 50 + i];
					var rootLodDistance =
						math.select(distanceScale * math.length(lodParams.cameraPos - cullData.position), distanceScale,
							isOrtho);
					var rootLodIntersect = (rootLodDistance < cullData.maxDistance) &&
					                       (rootLodDistance >= cullData.minDistance);

					if (rootLodIntersect)
					{
						var chunkIn = OtherUtils.Intersect2NoPartial(planes, cullData.bound);
						if (chunkIn != OtherUtils.IntersectResult.Out)
						{
							//设置batch的index
							indexList[bv.offset + visibleInstanceIndex] = i;
							visibleInstanceIndex++;
						}
					}
				}

				//设置batch的visibleCount
				bv.visibleCount = visibleInstanceIndex;
				batches[index] = bv;
			}
		}
	}
}