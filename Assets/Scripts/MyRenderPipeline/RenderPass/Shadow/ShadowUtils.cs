using UnityEngine;
using Utility;

namespace MyRenderPipeline.Shadow
{
	public static class ShadowUtils
	{
		
		public static Vector3[] GetCameraFrustumVerticies(Camera camera, float near, float far)
		{
			var frustumVerts = new Vector3[8];
			int idx = 0;
			var h = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2);
			var w = h * camera.aspect;
			for (int x = -1; x <= 1; x += 2)
			for (int y = -1; y <= 1; y+=2)
			{
				var p = new Vector3(w * x, h * y, 1);
				var pNear = camera.transform.localToWorldMatrix.MultiplyPoint(p * near);
				var pFar = camera.transform.localToWorldMatrix.MultiplyPoint(p * far);
				frustumVerts[idx++] = pNear;
				frustumVerts[idx++] = pFar;

				//Debug.DrawLine(pNear, pFar, Color.magenta);
			}
			return frustumVerts;
		}
		
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
		
		/// <summary>
		/// 画不规则灯光面板
		/// </summary>
		/// <param name="polygon"></param>
		/// <param name="lightViewProjection"></param>
		/// <param name="color"></param>
		/// <param name="z"></param>
		public static void DrawPolygonOnLightPlane(Vector2[] polygon, Matrix4x4 lightViewProjection, Color color, float z = 0)
		{
			var inverseVP = lightViewProjection.inverse;
			var p = lightViewProjection * new Vector4(0, 0, z, 1);
			var w = p.w;
			p /= w;
			for (var i = 0; i < polygon.Length; i++)
			{
				var p1 = inverseVP * polygon[i].ToVector4(p.z, 1) * w;
				var p2 = inverseVP * polygon[(i + 1) % polygon.Length].ToVector4(p.z, 1) * w;
				Debug.DrawLine(p1, p2, color);

			}
		}
	}
}