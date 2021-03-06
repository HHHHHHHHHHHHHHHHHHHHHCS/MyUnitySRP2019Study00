﻿using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.TAAURP
{
	[Serializable]
	public sealed class TAAURPQualityParameter : VolumeParameter<TAAURPQuality>
	{
		public TAAURPQualityParameter(TAAURPQuality value, bool overrideState = false)
			: base(value, overrideState)
		{
		}
	}


	[Serializable, VolumeComponentMenu("My/TAAURP")]
	public class TAAURPPostProcess : VolumeComponent, IPostProcessComponent
	{
		public BoolParameter enableEffect = new BoolParameter(false);

		[Tooltip("The quality of AntiAliasing")]
		public TAAURPQualityParameter quality = new TAAURPQualityParameter(TAAURPQuality.Low);

		[Tooltip("Sampling Distance")] public ClampedFloatParameter spread = new ClampedFloatParameter(1.0f, 0f, 1f);

		[Tooltip("Feedback")] public ClampedFloatParameter feedback = new ClampedFloatParameter(0.0f, 0f, 1f);

		public bool IsActive() => enableEffect.value && feedback.value > 0.0f; // && feedback.overrideState == true;

		public bool IsTileCompatible() => false;
	}
}