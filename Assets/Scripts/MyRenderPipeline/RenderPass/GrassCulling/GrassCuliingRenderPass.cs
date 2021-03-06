﻿using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.GrassCulling
{
	public class GrassCullingRenderPass : ScriptableRenderPass
	{
		private static readonly int _grassBendingRT_pid = Shader.PropertyToID("_GrassBendingRT");

		private static readonly RenderTargetIdentifier _grassBendingRT_rti =
			new RenderTargetIdentifier(_grassBendingRT_pid);

		private ShaderTagId grassBending_stid = new ShaderTagId("GrassBending");

		public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
		{
			//512*512足够了    
			//TODO:让rt 跟随主相机的view frustrum , 允许他们更智能的匹配尺寸
			cmd.GetTemporaryRT(_grassBendingRT_pid, new RenderTextureDescriptor(512, 512, RenderTextureFormat.R8, 0));
			ConfigureTarget(_grassBendingRT_rti);
			ConfigureClear(ClearFlag.All, Color.white);
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var instance = GrassCullingRenderer.instance;

			if (!instance)
			{
				//Debug.LogError("GrassCullingRenderer not found , abort GrassCullingRenderPass's Execute!");
				return;
			}

			CommandBuffer cmd = CommandBufferPool.Get("GrassBendingRT");

			var instanceTransform = instance.transform;
			
			//size z -1   unity 坐标左右手互换
			Matrix4x4 viewMatrix = Matrix4x4
				.TRS(instanceTransform.position + new Vector3(0, 1, 0),
					Quaternion.LookRotation(Vector3.down), new Vector3(1, 1, -1)).inverse;

			//ortho camera with 1:1 aspect, size = 50
			float sizeX = instanceTransform.localScale.x;
			float sizeZ = instanceTransform.localScale.z;
			Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-sizeX, sizeX, -sizeZ, sizeZ, 0.01f, 1.5f);

			//set view projection
			cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
			context.ExecuteCommandBuffer(cmd);

			var drawSetting =
				CreateDrawingSettings(grassBending_stid, ref renderingData, SortingCriteria.CommonTransparent);
			var filterSetting = new FilteringSettings(RenderQueueRange.all);
			context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filterSetting);
			context.ExecuteCommandBuffer(cmd);
			
			//复原view projection
			cmd.Clear();
			cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix,
				renderingData.cameraData.camera.projectionMatrix);

			cmd.SetGlobalTexture(_grassBendingRT_pid, _grassBendingRT_rti);

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}

		public override void FrameCleanup(CommandBuffer cmd)
		{
			cmd.ReleaseTemporaryRT(_grassBendingRT_pid);
		}
	}
}