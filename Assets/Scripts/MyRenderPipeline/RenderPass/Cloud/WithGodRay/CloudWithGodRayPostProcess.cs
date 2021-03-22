using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.WithGodRay
{
	[Serializable, VolumeComponentMenu("My/CloudWithGodRay")]
	public class CloudWithGodRayPostProcess : VolumeComponent, IPostProcessComponent
	{
		public BoolParameter enableEffect = new BoolParameter(false);

		//light
		//public FloatParameter numStepsLight = new FloatParameter { value = 6 };
		[Header("light==============")] public ColorParameter colA = new ColorParameter(Color.white);
		public ColorParameter colB = new ColorParameter(Color.white);
		public FloatParameter colorOffset1 = new FloatParameter(0.59f);
		public FloatParameter colorOffset2 = new FloatParameter(1.02f);
		public FloatParameter lightAbsorptionTowardSun = new FloatParameter(0.1f);
		public FloatParameter lightAbsorptionThroughCloud = new FloatParameter(1);
		public Vector4Parameter phaseParams = new Vector4Parameter(new Vector4(0.72f, 1, 0.5f, 1.58f));

		//density
		[Header("density==============")] public FloatParameter densityOffset = new FloatParameter(4.02f);
		public FloatParameter densityMultiplier = new FloatParameter(2.31f);
		public FloatParameter step = new FloatParameter(1.2f);
		public FloatParameter rayStep = new FloatParameter(1.2f);
		public FloatParameter rayOffsetStrength = new FloatParameter(1.5f);
		public ClampedIntParameter downsample = new ClampedIntParameter(4, 1, 16);
		public ClampedFloatParameter heightWeights = new ClampedFloatParameter(1, 0, 1);

		public Vector4Parameter shapeNoiseWeights = new Vector4Parameter
			(new Vector4(-0.17f, 27.17f, -3.65f, -0.08f));

		public FloatParameter detailWeights = new FloatParameter(-3.76f);
		public FloatParameter detailNoiseWeight = new FloatParameter(0.12f);

		public Vector4Parameter xy_Speed_zw_Warp = new Vector4Parameter(new Vector4(0.05f, 1, 1, 10));

		public bool IsActive() => enableEffect.value;

		public bool IsTileCompatible() => false;
	}
}