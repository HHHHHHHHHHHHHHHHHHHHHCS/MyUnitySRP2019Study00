using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.GodRay
{
	[Serializable, VolumeComponentMenu("My/GodRay")]
	public class GodRayPostProcess : VolumeComponent, IPostProcessComponent
	{
		public BoolParameter enableEffect = new BoolParameter(false);
		public Vector2Parameter godRayDir = new Vector2Parameter(-Vector2.one);
		public ClampedFloatParameter godRayStrength = new ClampedFloatParameter(0.5f, 0f, 4f);
		public ClampedFloatParameter godRayMaxDistance = new ClampedFloatParameter(1.0f, 0f, 2f);
		public ColorParameter godRayColor = new ColorParameter(Color.white);


		public bool IsActive() => enableEffect.value;

		public bool IsTileCompatible() => false;
	}
}