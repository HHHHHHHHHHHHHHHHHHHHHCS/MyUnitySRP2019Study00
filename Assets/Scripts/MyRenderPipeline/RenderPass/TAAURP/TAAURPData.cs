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
		public Matrix4x4 projPrevious;
		public Matrix4x4 viewPrevious;
		public Matrix4x4 projCurrent;
		public Matrix4x4 viewCurrent;
		
		public TAAURPData()
		{
			sampleOffset = Vector2.zero;
			proOverride = Matrix4x4.identity;
			projPrevious = Matrix4x4.identity;
			viewPrevious = Matrix4x4.identity;
			projCurrent = Matrix4x4.identity;
			viewCurrent = Matrix4x4.identity;
		}
	}
}
