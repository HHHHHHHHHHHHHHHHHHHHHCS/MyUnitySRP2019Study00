using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderPipeline
{
	public abstract class RenderPassRenderer<T> : RenderPass
		where T : RenderPassAsset
	{
		protected T asset { get; private set; }

		public RenderPassRenderer(T asset)
		{
			this.asset = asset;
		}
	}

	public abstract class RenderPass
	{
		[NonSerialized] private bool _reload = true;

		protected virtual void Init()
		{
		}

		public virtual void InternalSetup()
		{
			if (_reload)
			{
				Init();
				_reload = false;
			}
		}

		public virtual void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
		{
		}
	}
}