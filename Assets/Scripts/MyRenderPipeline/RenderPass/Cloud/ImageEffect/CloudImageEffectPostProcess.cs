using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	[Serializable, VolumeComponentMenu("My/CloudImageEffect")]
	public class CloudImageEffectPostProcess : VolumeComponent, IPostProcessComponent
	{
		public enum DebugMode
		{
			None = 0,
			Shape,
			Detail,
			Weather,
		}
		
		public enum ColorMask
		{
			R = 0,
			G,
			B,
			A,
		}

		[Serializable]
		public sealed class DebugModeParameter : VolumeParameter<DebugMode>
		{
			public DebugModeParameter(DebugMode value, bool overrideState = false)
				: base(value, overrideState)
			{
			}
		}
		
		[Serializable]
		public sealed class ColorMaskParameter : VolumeParameter<ColorMask>
		{
			public ColorMaskParameter(ColorMask value, bool overrideState = false)
				: base(value, overrideState)
			{
			}
		}
		
		public BoolParameter enableEffect = new BoolParameter(false);

		public BoolParameter followCamera = new BoolParameter(false);
		
		public BoolParameter useSkybox = new BoolParameter(true);
		
		private const string headerDecoration = " --- ";

		public Vector3Parameter cloudTestParams = new Vector3Parameter(Vector3.zero);

		[Header("March settings" + headerDecoration)]
		public IntParameter numStepsLight = new IntParameter(8);

		public FloatParameter rayOffsetStrength = new FloatParameter(0);

		
		
		[Header(headerDecoration + "Base Shape" + headerDecoration)]
		public FloatParameter cloudScale = new FloatParameter(1);

		public FloatParameter densityMultiplier = new FloatParameter(1);
		public FloatParameter densityOffset = new FloatParameter(1);

		public Vector3Parameter shapeOffset = new Vector3Parameter(Vector3.zero);
		public Vector2Parameter heightOffset = new Vector2Parameter(Vector2.zero);
		public Vector4Parameter shapeNoiseWeights = new Vector4Parameter(Vector4.zero);

		
		
		[Header(headerDecoration + "Detail" + headerDecoration)]
		public FloatParameter detailNoiseScale = new FloatParameter(10);

		public FloatParameter detailNoiseWeight = new FloatParameter(0.1f);

		public Vector3Parameter detailNoiseWeights = new Vector3Parameter(Vector3.zero);
		public Vector3Parameter detailOffset = new Vector3Parameter(Vector3.zero);

		
		
		
		[Header(headerDecoration + "Lighting" + headerDecoration)]
		public FloatParameter lightAbsorptionThroughCloud = new FloatParameter(1);

		public FloatParameter lightAbsorptionTowardSun = new FloatParameter(1);

		public ClampedFloatParameter darknessThreshold = new ClampedFloatParameter(0.2f, 0, 1);
		public ClampedFloatParameter forwardScattering = new ClampedFloatParameter(0.83f, 0, 1);
		public ClampedFloatParameter backScattering = new ClampedFloatParameter(0.3f, 0, 1);
		public ClampedFloatParameter baseBrightness = new ClampedFloatParameter(0.8f, 0, 1);
		public ClampedFloatParameter phaseFactor = new ClampedFloatParameter(0.15f, 0, 1);


		
		[Header(headerDecoration + "Animation" + headerDecoration)]
		public FloatParameter timeScale = new FloatParameter(1);

		public FloatParameter baseSpeed = new FloatParameter(1);
		public FloatParameter detailSpeed = new FloatParameter(2);

		
		
		[Header(headerDecoration + "Sky" + headerDecoration)]
		public ColorParameter colA = new ColorParameter(Color.black);

		public ColorParameter colB = new ColorParameter(Color.black);

		
		
		[Header(headerDecoration + "Debug" + headerDecoration)]
		public DebugModeParameter debugMode = new DebugModeParameter(DebugMode.None);
		
		public BoolParameter viewerShadowAllChannels = new BoolParameter(false);
		public ColorMaskParameter viewerColorMask = new ColorMaskParameter(ColorMask.R);
		public BoolParameter viewerGreyScale = new BoolParameter(true);
		public ClampedFloatParameter viewerSliceDepth = new ClampedFloatParameter(0, 0, 1);
		public ClampedFloatParameter viewerTileAmount = new ClampedFloatParameter(1, 1, 5);
		public ClampedFloatParameter viewerSize = new ClampedFloatParameter(1, 0, 1);


		public bool IsActive() => enableEffect.value;

		public bool IsTileCompatible() => false;
	}
}