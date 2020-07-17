using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline
{
	public abstract class MyUserPass : MonoBehaviour
	{
		public bool global = false;

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