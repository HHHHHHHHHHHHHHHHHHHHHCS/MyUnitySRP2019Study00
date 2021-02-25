using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.BatchRenderer
{
	//因为有一些Unity.Mathematics没有 在ECS包里面 所以抽出来了
	public class OtherUtils
	{
		//AABB--------------------
		public static float3 RotateExtents(float3 extents, float3 m0, float3 m1, float3 m2)
		{
			return math.abs(m0 * extents.x) + math.abs(m1 * extents.y) + math.abs(m2 * extents.z);
		}

		public static Bounds Transform(float4x4 transform, Bounds localBounds)
		{
			Bounds transformed = new Bounds(
				center: math.transform(transform, localBounds.center),
				size: 2 * RotateExtents(localBounds.extents, transform.c0.xyz, transform.c1.xyz, transform.c2.xyz)
			);
			return transformed;
		}

		//Plane--------------------
		public struct PlanePacket4
		{
			public float4 Xs;
			public float4 Ys;
			public float4 Zs;
			public float4 Distances;
		}


		public static NativeArray<PlanePacket4> BuildSOAPlanePackets(NativeArray<Plane> cullingPlanes,
			Allocator allocator)
		{
			int cullingPlaneCount = cullingPlanes.Length;
			int packetCount = (cullingPlaneCount + 3) >> 2;
			var planes = new NativeArray<PlanePacket4>(packetCount, allocator, NativeArrayOptions.UninitializedMemory);

			for (int i = 0; i < cullingPlaneCount; i++)
			{
				var p = planes[i >> 2];
				p.Xs[i & 3] = cullingPlanes[i].normal.x;
				p.Ys[i & 3] = cullingPlanes[i].normal.y;
				p.Zs[i & 3] = cullingPlanes[i].normal.z;
				p.Distances[i & 3] = cullingPlanes[i].distance;
				planes[i >> 2] = p;
			}

			// Populate the remaining planes with values that are always "in"
			for (int i = cullingPlaneCount; i < 4 * packetCount; ++i)
			{
				var p = planes[i >> 2];
				p.Xs[i & 3] = 1.0f;
				p.Ys[i & 3] = 0.0f;
				p.Zs[i & 3] = 0.0f;
				p.Distances[i & 3] = 32786.0f; //float.MaxValue;
				planes[i >> 2] = p;
			}

			return planes;
		}

		public static float4 dot4(float4 xs, float4 ys, float4 zs, float4 mx, float4 my, float4 mz)
		{
			return xs * mx + ys * my + zs * mz;
		}

		public enum IntersectResult
		{
			Out,
			In,
			Partial
		};
		
		public static IntersectResult Intersect2NoPartial(NativeArray<PlanePacket4> cullingPlanePackets, Bounds a)
		{
			float4 mx = a.center.x;
			float4 my = a.center.y;
			float4 mz = a.center.z;

			float4 ex = a.extents.x;
			float4 ey = a.extents.y;
			float4 ez = a.extents.z;

			int4 masks = 0;

			for (int i = 0; i < cullingPlanePackets.Length; i++)
			{
				var p = cullingPlanePackets[i];
				float4 distances = dot4(p.Xs, p.Ys, p.Zs, mx, my, mz) + p.Distances;
				float4 radii = dot4(ex, ey, ez, math.abs(p.Xs), math.abs(p.Ys), math.abs(p.Zs));

				masks += (int4) (distances + radii <= 0);
			}

			int outCount = math.csum(masks);
			return outCount > 0 ? IntersectResult.Out : IntersectResult.In;
		}


		//LOD--------------------
		public struct LODParams : IEqualityComparer<LODParams>, IEquatable<LODParams>
		{
			public float distanceScale;
			public float3 cameraPos;

			public bool isOrtho;
			public float orthosize;

			public bool Equals(LODParams x, LODParams y)
			{
				return
					x.distanceScale == y.distanceScale &&
					x.cameraPos.Equals(y.cameraPos) &&
					x.isOrtho == y.isOrtho &&
					x.orthosize == y.orthosize;
			}

			public bool Equals(LODParams x)
			{
				return
					x.distanceScale == distanceScale &&
					x.cameraPos.Equals(cameraPos) &&
					x.isOrtho == isOrtho &&
					x.orthosize == orthosize;
			}

			public int GetHashCode(LODParams obj)
			{
				throw new System.NotImplementedException();
			}
		}

		public static float CalculateLodDistanceScale(float fieldOfView, float globalLodBias, bool isOrtho,
			float orthoSize)
		{
			float distanceScale;
			if (isOrtho)
			{
				distanceScale = 2.0f * orthoSize / globalLodBias;
			}
			else
			{
				var halfAngle = math.tan(math.radians(fieldOfView * 0.5F));
				// Half angle at 90 degrees is 1.0 (So we skip halfAngle / 1.0 calculation)
				distanceScale = (2.0f * halfAngle) / globalLodBias;
			}

			return distanceScale;
		}

		public static LODParams CalculateLODParams(LODParameters parameters, float overrideLODBias = 0.0f)
		{
			LODParams lodParams;
			lodParams.cameraPos = parameters.cameraPosition;
			lodParams.isOrtho = parameters.isOrthographic;
			lodParams.orthosize = parameters.orthoSize;
			if (overrideLODBias == 0.0F)
				lodParams.distanceScale = CalculateLodDistanceScale(parameters.fieldOfView, QualitySettings.lodBias,
					lodParams.isOrtho, lodParams.orthosize);
			else
			{
				// overrideLODBias is not affected by FOV etc
				// This is useful if the FOV is continously changing (breaking LOD temporal cache) or you want to explicit control LOD bias.
				lodParams.distanceScale = 1.0F / overrideLODBias;
			}

			return lodParams;
		}
	}
}