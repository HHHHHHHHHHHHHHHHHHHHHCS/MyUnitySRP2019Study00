using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.CrepuscularRay
{
	[Serializable, VolumeComponentMenu("My/CrepuscularRay")]
	public class CrepuscularRayPostProcess : VolumeComponent, IPostProcessComponent
	{
		public enum QualityMode
		{
			defaultQuality = 0,
			customQuality
		}

		[Serializable]
		public sealed class QualityModeParameter : VolumeParameter<QualityMode>
		{
			public QualityModeParameter(QualityMode value, bool overrideState = false)
				: base(value, overrideState)
			{
			}
		}

		public BoolParameter enableEffect = new BoolParameter(false);

		public QualityModeParameter qualityMode = new QualityModeParameter(QualityMode.defaultQuality);

		public ColorParameter lightColor = new ColorParameter(new Color(1.33f, 0.98f, 0.69f, 1));
		public ClampedFloatParameter rayRange = new ClampedFloatParameter(0.94f, 0f, 2f);

		public FloatParameter rayIntensity = new FloatParameter(2);
		public ClampedFloatParameter rayPower = new ClampedFloatParameter(1.25f, 1f, 3f);
		public ClampedFloatParameter lightThreshold = new ClampedFloatParameter(0.29f, 0, 1);
		public ClampedIntParameter qualityStep = new ClampedIntParameter(32, 2, 64);
		public ClampedFloatParameter offsetUV = new ClampedFloatParameter(0.027f, 0, 0.1f);
		public ClampedFloatParameter boxBlur = new ClampedFloatParameter(0.00126f, 0f, 0.01f);
		public ClampedIntParameter downsample = new ClampedIntParameter(4, 1, 16);


		public bool IsActive() => enableEffect.value;

		public bool IsTileCompatible() => false;
	}
}