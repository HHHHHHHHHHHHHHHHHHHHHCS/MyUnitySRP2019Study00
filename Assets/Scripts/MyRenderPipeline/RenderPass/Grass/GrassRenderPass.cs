using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GrassRenderPass : ScriptableRenderPass
{
	private static readonly int grassBendingRT_pid = Shader.PropertyToID("_GrassBendingRT");

	private static readonly RenderTargetIdentifier
		grassBendingRT_rti = new RenderTargetIdentifier(grassBendingRT_pid);

	private ShaderTagId grassBending_stid = new ShaderTagId("GrassBending");

	public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
	{
		//512*512对于这个演示的max grass count来说足够大了，在常规用例中可以使用更小的RT
		//TODO:使RT渲染位置跟随主摄影机视图frustrum，允许使用更小的RT大小
		cmd.GetTemporaryRT(grassBendingRT_pid, new RenderTextureDescriptor(512, 512, RenderTextureFormat.R8, 0));
		//别忘记清理RT
		ConfigureTarget(grassBendingRT_rti);
		ConfigureClear(ClearFlag.All, Color.white);
	}

	public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
	{
		if (!InstancedIndirectGrassRenderer.instance)
		{
			Debug.LogWarning("InstancedIndirectGrassRenderer not found, abort GrassBendingRTPrePass's Execute");
			return;
		}

		CommandBuffer cmd = CommandBufferPool.Get("GrassBendingRT");

		Transform grassRoot = InstancedIndirectGrassRenderer.instance.transform;

		//创建一个新的视图矩阵，它与草地中心1单位上方的假想摄影机相同，并观察草地（鸟瞰图）
		//scale.z是-1，因为视图空间将查看-z，而世界空间将查看+z
		//Matrix4x4 是 local->world的inverse = world->local = world->camera local = viewMatrix
		Matrix4x4 viewMatrix = Matrix4x4
			.TRS(grassRoot.position + new Vector3(0, 1.0f, 0),
				Quaternion.LookRotation(Vector3.down), new Vector3(1, 1, -1)).inverse;

		//ortho camera with 1:1 aspect, size = 50
		float sizeX = grassRoot.localScale.x;
		float sizeZ = grassRoot.localScale.z;
		Matrix4x4 projectionMatrix = Matrix4x4.Ortho(-sizeX, sizeX, -sizeZ, sizeZ, 0.01f, 1.5f);

		cmd.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
		context.ExecuteCommandBuffer(cmd);

		var drawSetting =
			CreateDrawingSettings(grassBending_stid, ref renderingData, SortingCriteria.CommonTransparent);
		var filterSetting = new FilteringSettings(RenderQueueRange.all);
		context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filterSetting);

		context.ExecuteCommandBuffer(cmd);
		cmd.Clear();
		cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
		
		//set global RT
		cmd.SetGlobalTexture(grassBendingRT_pid, grassBendingRT_rti);

		context.ExecuteCommandBuffer(cmd);
		CommandBufferPool.Release(cmd);
	}

	public override void FrameCleanup(CommandBuffer cmd)
	{
		cmd.ReleaseTemporaryRT(grassBendingRT_pid);
	}
}