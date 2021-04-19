using UnityEngine;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	public enum TAAURPQuality
	{
		Low,
		Medium,
		High,
	}
	
	public class TAAURPData
	{
		public Vector2 sampleOffset;
		public Matrix4x4 proOverride;
		public Matrix4x4 projPreview;
		public Matrix4x4 viewPreview;

		public TAAURPData()
		{
			sampleOffset = Vector2.zero;
			proOverride = Matrix4x4.identity;
			projPreview = Matrix4x4.identity;
			viewPreview = Matrix4x4.identity;
		}
	}
}
