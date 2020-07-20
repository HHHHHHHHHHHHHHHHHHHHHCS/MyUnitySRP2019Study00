using UnityEngine;

namespace MyRenderPipeline.Shadow
{
	public static class ShadowUtils
	{
		
		/// <summary>
		/// 画出Frustum框
		/// </summary>
		/// <param name="frustum"></param>
		/// <param name="orthographic"></param>
		/// <param name="transform"></param>
		public static void DrawFrustum(FrustumPlanes frustum, bool orthographic, Matrix4x4 transform)
		{
			var verts = new Vector3[]
			{
				new Vector3(frustum.left, frustum.bottom, frustum.zNear),
				new Vector3(frustum.right, frustum.bottom, frustum.zNear),
				new Vector3(frustum.right, frustum.top, frustum.zNear),
				new Vector3(frustum.left, frustum.top, frustum.zNear),
				new Vector3(frustum.left, frustum.bottom, frustum.zNear),
			};
			if (orthographic)
			{
				var extend = Vector3.forward * (frustum.zFar - frustum.zNear);
				for (var i = 0; i < 4; i++)
				{
					Debug.DrawLine(transform.MultiplyPoint(verts[i]), transform.MultiplyPoint(verts[i + 1]),
						Color.yellow);
					Debug.DrawLine(transform.MultiplyPoint(verts[i]), transform.MultiplyPoint(verts[i] + extend),
						Color.green);
					Debug.DrawLine(transform.MultiplyPoint(verts[i] + extend),
						transform.MultiplyPoint(verts[i + 1] + extend), Color.green);
				}
			}
			else
			{
				for (var i = 0; i < 4; i++)
				{
					Debug.DrawLine(transform.MultiplyPoint(verts[i]), transform.MultiplyPoint(verts[i + 1]),
						Color.yellow);
					Debug.DrawLine(transform.MultiplyPoint(verts[i]),
						transform.MultiplyPoint(verts[i] * frustum.zFar / frustum.zNear), Color.green);
					Debug.DrawLine(transform.MultiplyPoint(verts[i] * frustum.zFar / frustum.zNear),
						transform.MultiplyPoint(verts[i + 1] * frustum.zFar / frustum.zNear), Color.green);
				}
			}
		}

		/// <summary>
		/// 画出bound的12条边
		/// </summary>
		/// <param name="bounds"></param>
		/// <param name="color"></param>
		public static void DrawBound(Bounds bounds, Color color)
		{
			var verts = new Vector2[]
			{
				new Vector2(-1, -1),
				new Vector2(1, -1),
				new Vector2(1, 1),
				new Vector2(-1, 1),
				new Vector2(-1, -1),
			};
			for (var i = 0; i < 4; i++)
			{
				Debug.DrawLine(bounds.center + Vector3.Scale(bounds.extents, new Vector3(verts[i].x, verts[i].y, 1)),
					bounds.center + Vector3.Scale(bounds.extents, new Vector3(verts[i + 1].x, verts[i + 1].y, 1)),
					color);
				Debug.DrawLine(bounds.center + Vector3.Scale(bounds.extents, new Vector3(verts[i].x, verts[i].y, -1)),
					bounds.center + Vector3.Scale(bounds.extents, new Vector3(verts[i + 1].x, verts[i + 1].y, -1)),
					color);

				Debug.DrawLine(bounds.center + Vector3.Scale(bounds.extents, new Vector3(verts[i].x, verts[i].y, 1)),
					bounds.center + Vector3.Scale(bounds.extents, new Vector3(verts[i].x, verts[i].y, -1)), color);
			}
		}
	}
}