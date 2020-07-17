using UnityEngine;

namespace MyRenderPipeline.Shadow
{
	public struct MyShadowMapData
	{
		public int shadowMapIdentifier;
		public Matrix4x4 world2Light;
		public Matrix4x4 postTransform; //TSM矩阵用
		public float bias;
		public ShadowAlgorithms shadowType;
		public Vector4 shadowParameters;
	}
}