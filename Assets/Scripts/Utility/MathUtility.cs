using UnityEngine;

namespace Utility
{
	public static class MathUtility
	{
		public static Vector2 ToVector2(this Vector3 v) => new Vector2(v.x, v.y);

		public static Vector4 ToVector4(this Vector2 v, float z = 0, float w = 0)
			=> new Vector4(v.x, v.y, z, w);

		public static Vector4 ToVector4(this Vector3 v, float w = 0)
			=> new Vector4(v.x, v.y, v.z, w);
	}
}