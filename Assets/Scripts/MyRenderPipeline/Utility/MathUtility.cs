using UnityEngine;

namespace Utility
{
	public static class MathUtility
	{
		public static Vector2 ToVector2(this Vector3 v) => new Vector2(v.x, v.y);

		public static Vector3 ToVector3(this Vector4 v) => new Vector3(v.x, v.y, v.z);


		public static Vector4 ToVector4(this Vector2 v, float z = 0, float w = 0)
			=> new Vector4(v.x, v.y, z, w);

		public static Vector4 ToVector4(this Vector3 v, float w = 0)
			=> new Vector4(v.x, v.y, v.z, w);
		
		public static float Cross2(Vector2 u, Vector2 v)
			=> u.x * v.y - u.y * v.x;
	}
}