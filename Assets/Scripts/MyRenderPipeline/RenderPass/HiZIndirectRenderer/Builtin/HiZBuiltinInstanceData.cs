using UnityEngine;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.Builtin
{
	[System.Serializable]
	public class HiZBuiltinInstanceData
	{
		public GameObject prefab;
		public Material indirectMaterial;
		public Vector2 scaleRange;
		public Vector3 positionOffset;
		public Mesh lod00Mesh;
		public Mesh lod01Mesh;
		public Mesh lod02Mesh;
		public Vector3[] rotations;
		public Vector3[] positions;
		public Vector3[] scales;
	}
}
