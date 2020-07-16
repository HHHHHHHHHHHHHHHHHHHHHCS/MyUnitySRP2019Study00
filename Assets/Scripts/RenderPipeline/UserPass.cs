using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderPipeline
{
	public abstract class UserPass : MonoBehaviour
	{
		public bool global = false;

		public virtual void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
		{
		}

		public virtual void Render(ScriptableRenderContext context, ref RenderingData renderingData)
		{
		}

		public virtual void Cleanup(ScriptableRenderContext context, ref RenderingData renderingData)
		{
		}
	}
}