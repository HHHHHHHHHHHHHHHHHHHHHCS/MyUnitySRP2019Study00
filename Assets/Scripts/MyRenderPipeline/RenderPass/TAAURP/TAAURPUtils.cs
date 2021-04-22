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

			while (index > 0)
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


		public static Matrix4x4 GetJitteredOrthographicProjectionMatrix(Camera camera, Vector2 offset)
		{
			float vertical = camera.orthographicSize;
			float horizontal = vertical * camera.aspect;

			offset.x *= horizontal / (0.5f * camera.scaledPixelWidth);
			offset.y *= vertical / (0.5f * camera.scaledPixelHeight);

			float left = offset.x - horizontal;
			float right = offset.x + horizontal;
			float top = offset.y - vertical;
			float bottom = offset.y + vertical;

			return Matrix4x4.Ortho(left, right, bottom, top, camera.nearClipPlane, camera.farClipPlane);
		}

		public static Matrix4x4 GetJitteredPerspectiveProjectionMatrix(Camera camera, Vector2 offset)
		{
			float near = camera.nearClipPlane;
			// float far = camera.farClipPlane;

			float vertical = Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad) * near;
			float horizontal = vertical * camera.aspect;

			offset.x *= horizontal / (0.5f * camera.scaledPixelWidth);
			offset.y *= vertical / (0.5f * camera.scaledPixelHeight);

			var matrix = camera.projectionMatrix;

			matrix[0, 2] += offset.x / horizontal;
			matrix[1, 2] += offset.y / vertical;

			return matrix;
		}
	}
}