﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.HiZIndirectRenderer.SRP
{
	public class HiZSRPDebugOpaquePass : ScriptableRenderPass
	{
		FilteringSettings m_FilteringSettings;
		RenderStateBlock m_RenderStateBlock;
		List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
		string m_ProfilerTag;
		ProfilingSampler m_ProfilingSampler;
		bool m_IsOpaque;

		static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

		public HiZSRPDebugOpaquePass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange,
			LayerMask layerMask, StencilState stencilState, int stencilReference)
		{
			m_ProfilerTag = profilerTag;
			m_ProfilingSampler = new ProfilingSampler(profilerTag);
			m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
			m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
			m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
			renderPassEvent = evt;

			m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
			m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
			m_IsOpaque = opaque;

			if (stencilState.enabled)
			{
				m_RenderStateBlock.stencilReference = stencilReference;
				m_RenderStateBlock.mask = RenderStateMask.Stencil;
				m_RenderStateBlock.stencilState = stencilState;
			}
		}

		/// <inheritdoc/>
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			//TODO:DEBUG  set camera  set viewport
			CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
			using (new ProfilingScope(cmd, m_ProfilingSampler))
			{
				// Global render pass data containing various settings.
				// x,y,z are currently unused
				// w is used for knowing whether the object is opaque(1) or alpha blended(0)
				Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (m_IsOpaque) ? 1.0f : 0.0f);
				cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();

				Camera camera = renderingData.cameraData.camera;
				var sortFlags = (m_IsOpaque)
					? renderingData.cameraData.defaultOpaqueSortFlags
					: SortingCriteria.CommonTransparent;
				var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
				var filterSettings = m_FilteringSettings;

#if UNITY_EDITOR
				// When rendering the preview camera, we want the layer mask to be forced to Everything
				if (renderingData.cameraData.camera.cameraType == CameraType.Preview)
				{
					filterSettings.layerMask = -1;
				}
#endif

				context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings,
					ref m_RenderStateBlock);

				// Render objects that did not match any shader pass with error shader
				// RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, filterSettings,
				// 	SortingCriteria.None);
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
	}
}