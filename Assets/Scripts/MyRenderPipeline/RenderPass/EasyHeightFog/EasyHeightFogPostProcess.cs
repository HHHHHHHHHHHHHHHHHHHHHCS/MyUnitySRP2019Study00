using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.EasyHeightFog
{
	[Serializable, VolumeComponentMenu("My/EasyHeightFog")]
	public class EasyHeightFogPostProcess : VolumeComponent, IPostProcessComponent
	{
		// 雾 1
		// This is the global density factor, which can be thought of as the fog layer's thickness.
		[Header("雾浓度")] public ClampedFloatParameter fogDensity = new ClampedFloatParameter(0.02f, 0.0f, 0.05f);

		// Height density factor, controls how the density increases as height decreases. Smaller values make the transition larger.
		[Header("雾高度衰减系数"),]
		public ClampedFloatParameter fogHeightFalloff = new ClampedFloatParameter(0.02f, 0.001f, 0.1f);

		[Header("雾高度")] public FloatParameter fogHeight = new FloatParameter(0.0f);

		// 雾 2
		[Header("雾浓度 2")] public ClampedFloatParameter fogDensity2 = new ClampedFloatParameter(0.02f, 0.0f, 0.05f);

		[Header("雾高度衰减系数 2")]
		public ClampedFloatParameter fogHeightFalloff2 = new ClampedFloatParameter(0.02f, 0.001f, 0.1f);

		[Header("雾高度 2")] public FloatParameter fogHeight2 = new FloatParameter(0.0f);

		// Sets the inscattering color for the fog. Essentially, this is the fog's primary color.
		[Header("雾色")] public ColorParameter fogInscatteringColor = new ColorParameter(
			new Color(0.447f, 0.639f, 1.0f, 1.0f), false, false, true);

		// This controls the maximum opacity of the fog. A value of 1 means the fog will be completely opaque, while 0 means the fog will be essentially invisible.
		[Header("雾最大不透明度")] public ClampedFloatParameter fogMaxOpacity = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);

		// Distance from the camera that the fog will start.
		[Header("雾开始距离")] public ClampedFloatParameter startDistance = new ClampedFloatParameter(0.0f, 0.0f, 5000.0f);

		[Header("雾终止距离")]
		public ClampedFloatParameter fogCutoffDistance = new ClampedFloatParameter(0.0f, 0.0f, 20000000.0f);

		// Controls the size of the directional inscattering cone, which is used to approximate inscattering from a directional light source.
		[Header("方向光范围系数")]
		public ClampedFloatParameter directionalInscatteringExponent = new ClampedFloatParameter(4.0f, 2.0f, 64.0f);

		// Controls the start distance from the viewer of the directional inscattering, which is used to approximate inscattering from a directional light.
		[Header("方向光影响开始距离")] public FloatParameter directionalInscatteringStartDistance = new FloatParameter(0.0f);

		// Sets the color for directional inscattering, used to approximate inscattering from a directional light. This is similar to adjusting the simulated color of a directional light source.
		[Header("方向光颜色")] public ColorParameter directionalInscatteringColor =
			new ColorParameter(new Color(0.25f, 0.25f, 0.125f), false, false, true);

		[Range(0.0f, 10.0f), Header("方向光强度")]
		public ClampedFloatParameter directionalInscatteringIntensity = new ClampedFloatParameter(1.0f, 0.0f, 10.0f);


		public bool IsActive() => active ;

		public bool IsTileCompatible() => false;
	}
}