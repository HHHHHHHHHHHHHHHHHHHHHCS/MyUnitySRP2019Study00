using MyRenderPipeline.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.Shadow
{
	public partial class ShadowPassRenderer
	{
		private MyShadowMapData StandardShadowMap(ScriptableRenderContext context, MyRenderingData renderingData,
			ShadowSettings settings, int lightIndex)
		{
			var cmd = CommandBufferPool.Get();
			cmd.Clear();
			var depthBuf = IdentifierPool.Get();

			cmd.GetTemporaryRT(depthBuf, settings.resolution, settings.resolution, 32, FilterMode.Point,
				RenderTextureFormat.Depth);

			RenderTargetBinding binding = new RenderTargetBinding();
			binding.depthRenderTarget = depthBuf;
			cmd.SetRenderTarget(depthBuf);
			cmd.ClearRenderTarget(true, true, Color.black);

			MyShadowMapData shadowMapData = new MyShadowMapData()
			{
				shadowMapIdentifier = depthBuf,
				bias = settings.bias,
				shadowType = ShadowAlgorithms.Standard,
			};

			var (view, projection) = GetShadowViewProjection(settings, renderingData, lightIndex);

			cmd.SetViewProjectionMatrices(view, projection);

			shadowMapData.world2Light = projection * view;

			cmd.SetGlobalDepthBias(settings.depthBias, settings.normalBias);

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();

			DrawShadowCasters(context, renderingData, shadowMapData, PassSimple);

			cmd.SetViewProjectionMatrices(renderingData.camera.worldToCameraMatrix,
				renderingData.camera.projectionMatrix);

			context.ExecuteCommandBuffer(cmd);
			cmd.Clear();


			CommandBufferPool.Release(cmd);

			return shadowMapData;
		}

		public static (Matrix4x4 view, Matrix4x4 projection) GetShadowViewProjection(ShadowSettings settings,
			MyRenderingData renderingData, int lightIndex)
		{
			var camera = renderingData.camera;
			if (settings.debug)
				camera = GameObject.Find("Main Camera").GetComponent<Camera>();
			var cameraToWorld = camera.transform.localToWorldMatrix;
			var p0 = cameraToWorld.MultiplyPoint(new Vector3(0, 0, 0));
			var h = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad / 2);
			var w = h * camera.aspect;

			var p1 = cameraToWorld.MultiplyPoint(new Vector3(-w, -h, 1) * settings.maxShadowDistance);
			var p2 = cameraToWorld.MultiplyPoint(new Vector3(w, -h, 1) * settings.maxShadowDistance);
			var p3 = cameraToWorld.MultiplyPoint(new Vector3(w, h, 1) * settings.maxShadowDistance);
			var p4 = cameraToWorld.MultiplyPoint(new Vector3(-w, h, 1) * settings.maxShadowDistance);

			//左右手坐标转换
			var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * settings.transform.worldToLocalMatrix;
			renderingData.cullResults.GetShadowCasterBounds(lightIndex, out var bounds);

			//阴影包围盒
			var casterBoundVerts = new Vector3[8];
			for (int x = -1, i = 0; x <= 1; x += 2)
			for (int y = -1; y <= 1; y += 2)
			for (int z = -1; z <= 1; z += 2)
				casterBoundVerts[i++] = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z));

			var debug = camera.name == "Main Camera" && settings.debug;
			if (settings.light.type == LightType.Point)
			{
				//朝向 + shadow框 
				var rotation = Matrix4x4.Rotate(Quaternion.FromToRotation(Vector3.forward,
					settings.transform.worldToLocalMatrix.MultiplyPoint(bounds.center).normalized));
				var rotatedView = rotation.inverse * settings.light.transform.worldToLocalMatrix;
				var frustumCaster = BestfitFrustum(false, rotatedView, casterBoundVerts);

				if (debug)
				{
					ShadowUtils.DrawBound(bounds, Color.cyan);
					ShadowUtils.DrawFrustum(frustumCaster, false, rotatedView.inverse);
				}

				return (Matrix4x4.Scale(new Vector3(1, 1, -1)) * rotatedView, Matrix4x4.Frustum(frustumCaster));
			}
			else if (settings.light.type == LightType.Spot)
			{
				Matrix4x4 lightView = settings.light.transform.worldToLocalMatrix;
				var frustumCamera = BestfitFrustum(false, lightView, p0, p1, p2, p3, p4);
				var frustumCaster = BestfitFrustum(false, lightView, casterBoundVerts);
				var near = settings.nearDistance;
				var far = Mathf.Min(frustumCaster.zFar, frustumCamera.zFar, settings.light.range);

				return (view, Matrix4x4.Perspective(settings.light.spotAngle, 1, near, far));
			}
			else if (settings.light.type == LightType.Directional)
			{
				//shadow框和摄像机框 比较 选择最大/小的
				var frustumCamera =
					BestfitFrustum(true, settings.light.transform.worldToLocalMatrix, p0, p1, p2, p3, p4);
				var frustumCaster = BestfitFrustum(true, settings.light.transform.worldToLocalMatrix, casterBoundVerts);

				var left = Mathf.Max(frustumCaster.left, frustumCamera.left);
				var right = Mathf.Min(frustumCaster.right, frustumCamera.right);
				var bottom = Mathf.Max(frustumCaster.bottom, frustumCamera.bottom);
				var top = Mathf.Min(frustumCaster.top, frustumCamera.top);
				var zNear = frustumCaster.zNear;
				var zFar = Mathf.Min(frustumCaster.zFar, frustumCamera.zFar);

				if (debug)
				{
					var bound = new Bounds();
					bound.min = new Vector3(left, bottom, zNear);
					bound.max = new Vector3(right, top, zFar);
					ShadowUtils.DrawFrustum(new FrustumPlanes()
					{
						left = Mathf.Max(frustumCaster.left, frustumCamera.left),
						right = Mathf.Min(frustumCaster.right, frustumCamera.right),
						bottom = Mathf.Max(frustumCaster.bottom, frustumCamera.bottom),
						top = Mathf.Min(frustumCaster.top, frustumCamera.top),
						zNear = frustumCaster.zNear,
						zFar = Mathf.Min(frustumCaster.zFar, frustumCamera.zFar),
					}, true, settings.transform.localToWorldMatrix);
					//DrawBound(bounds, Color.cyan);
					Debug.DrawRay(
						settings.transform.localToWorldMatrix.MultiplyPoint(new Vector3(bound.center.x, bound.center.y,
							zNear)), settings.transform.forward * 5, Color.green);
				}

				return (view, Matrix4x4.Ortho(left, right, bottom, top, zNear, zFar));
			}

			return (Matrix4x4.identity, Matrix4x4.identity);
		}


		public static FrustumPlanes BestfitFrustum(bool orthographic, Matrix4x4 transform, params Vector3[] verts)
		{
			FrustumPlanes frustum = new FrustumPlanes()
			{
				left = float.MaxValue,
				right = float.MinValue,
				bottom = float.MaxValue,
				top = float.MinValue,
				zNear = float.MaxValue,
				zFar = float.MinValue,
			};
			if (orthographic)
			{
				for (var i = 0; i < verts.Length; i++)
				{
					var transformedP = transform.MultiplyPoint(verts[i]);
					frustum.left = Mathf.Min(frustum.left, transformedP.x);
					frustum.bottom = Mathf.Min(frustum.bottom, transformedP.y);
					frustum.zNear = Mathf.Min(frustum.zNear, transformedP.z);
					frustum.right = Mathf.Max(frustum.right, transformedP.x);
					frustum.top = Mathf.Max(frustum.top, transformedP.y);
					frustum.zFar = Mathf.Max(frustum.zFar, transformedP.z);
				}
			}
			else
			{
				//单位框
				for (var i = 0; i < verts.Length; i++)
				{
					var transformedP = transform.MultiplyPoint(verts[i]);
					frustum.left = Mathf.Min(frustum.left, transformedP.x / transformedP.z);
					frustum.right = Mathf.Max(frustum.right, transformedP.x / transformedP.z);
					frustum.bottom = Mathf.Min(frustum.bottom, transformedP.y / transformedP.z);
					frustum.top = Mathf.Max(frustum.top, transformedP.y / transformedP.z);
					frustum.zNear = Mathf.Min(frustum.zNear, transformedP.z);
					frustum.zFar = Mathf.Max(frustum.zFar, transformedP.z);
				}

				//单位框 缩放 near
				frustum.left *= frustum.zNear;
				frustum.right *= frustum.zNear;
				frustum.bottom *= frustum.zNear;
				frustum.top *= frustum.zNear;
			}

			return frustum;
		}
	}
}