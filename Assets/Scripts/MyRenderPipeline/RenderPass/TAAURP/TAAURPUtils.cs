using UnityEngine;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	public static class TAAURPUtils
	{
		private const int k_SampleCount = 8;
		
		public static int SampleIndex { get; private set; } = 0;
		
		public static float GetRandom(int index, int radix)
		{
			float result = 0f;
			float fraction = 1.0f / radix;

			while (index>0)
			{
				result += (index % radix) * fraction;

				index /= radix;
				fraction /= radix;
			}

			return result;
		}

		public static Vector2 GenerateRandomOffset()
		{
			var offset = new Vector2(
				GetRandom((SampleIndex & 1023) + 1, 2) - 0.5f,
				GetRandom((SampleIndex & 1023) + 1, 3) - 0.5f
			);

			if ((++SampleIndex) >= k_SampleCount)
			{
				SampleIndex = 0;
			}

			return offset;
		}
	}
}