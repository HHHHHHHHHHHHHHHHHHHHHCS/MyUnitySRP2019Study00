using System;
using UnityEngine.Rendering;

namespace MyRenderPipeline.RenderPass.Common
{
	public abstract class MyRenderPassRenderer<T> : MyRenderPass
		where T : MyRenderPassAsset
	{
		protected T asset { get; private set; }

		public MyRenderPassRenderer(T asset)
		{
			this.asset = asset;
		}
	}

	public abstract class MyRenderPass
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

		public virtual void Setup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{
		}
		
		public virtual void Render(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{

		}

		public virtual void Cleanup(ScriptableRenderContext context, ref MyRenderingData renderingData)
		{

		}
	}
}