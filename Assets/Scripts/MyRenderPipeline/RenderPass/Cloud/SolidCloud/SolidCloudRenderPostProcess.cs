using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud
{
	[Serializable, VolumeComponentMenu("My/SolidCloud")]
	public class SolidCloudRenderPostProcess : VolumeComponent, IPostProcessComponent
	{
		public BoolParameter enableEffect = new BoolParameter(false);

		public BoolParameter enableBlend = new BoolParameter(true);

		public BoolParameter enableFrame = new BoolParameter(false);
		public ClampedIntParameter frameMode = new ClampedIntParameter(0, 0, 3);

		public BoolParameter mulRTBlend = new BoolParameter(false);
		public ClampedIntParameter rtSize = new ClampedIntParameter(1, 1, 5);
		public BoolParameter enableBlur = new BoolParameter(false);


		public BoolParameter useXYPlane = new BoolParameter(false);

		[Header("Mask")] public BoolParameter enableMask = new BoolParameter(false);
		public TextureParameter maskTexture = new TextureParameter(null);


		//float4 _CloudData ( y z w)   x is _cloudAreaPosition.y 
		[Header("CloudData")] public MinFloatParameter height = new MinFloatParameter(4, 0);
		public MinFloatParameter density = new MinFloatParameter(1, 0);
		public MinFloatParameter noiseScale = new MinFloatParameter(1, 0);


		//float4 _CloudDistance;
		[Header("CloudDistance")] public ClampedFloatParameter distance = new ClampedFloatParameter(100, 0, 1000);
		public ClampedFloatParameter distanceFallOff = new ClampedFloatParameter(0, 0, 5);
		public ClampedFloatParameter maxLength = new ClampedFloatParameter(2000, 0, 2000);
		public ClampedFloatParameter maxLengthFallOff = new ClampedFloatParameter(0, 0, 1);

		//float3 _CloudWindDir
		[Header("CloudWindDir")] public Vector3Parameter windDirection = new Vector3Parameter(Vector3.zero);
		public ClampedFloatParameter windSpeed = new ClampedFloatParameter(0, -100, 100);
		public MinFloatParameter noiseSpeed = new MinFloatParameter(1, 0);

		//alpha 藏在里面了
		[Header("CloudColor")]
		public ColorParameter cloudAlbedoColor = new ColorParameter(Color.white * 0.85f, true, true, true);

		public ColorParameter cloudSpecularColor = new ColorParameter(Color.white, true, true, true);


		[Header("CloudShape")] public Vector3Parameter cloudAreaPosition = new Vector3Parameter(Vector3.zero);

		//float4 _CloudAreaData
		public ClampedFloatParameter cloudAreaRadius = new ClampedFloatParameter(1, 0, 2000f);
		public ClampedFloatParameter cloudAreaHeight = new ClampedFloatParameter(0, 0, 2000);
		public ClampedFloatParameter cloudAreaDepth = new ClampedFloatParameter(0, 0, 2000);
		public ClampedFloatParameter cloudAreaFallOff = new ClampedFloatParameter(1.0f, 0, 10);

		//half3 _SunShadowsData
		[Header("SunShadow")] public ClampedFloatParameter sunShadowsStrength = new ClampedFloatParameter(0, 0, 1);
		public ClampedFloatParameter sunShadowsJitterStrength = new ClampedFloatParameter(0.1f, 0f, 0.5f);
		public ClampedFloatParameter sunShadowsCancellation = new ClampedFloatParameter(0, 0, 1);

		//float4 _CloudStepping (x y w)
		[Header("CloudStepping")] public ClampedFloatParameter stepping = new ClampedFloatParameter(8, 0, 20);
		public ClampedFloatParameter steppingNear = new ClampedFloatParameter(50, 0, 50);
		public ClampedFloatParameter ditherStrength = new ClampedFloatParameter(0, 0, 5);

		[Header("Noise")] public ClampedIntParameter noisePowSize = new ClampedIntParameter(7, 0, 12);
		public ClampedFloatParameter noiseStrength = new ClampedFloatParameter(0.95f, 0, 0.95f);
		public ClampedFloatParameter noiseDensity = new ClampedFloatParameter(1.25f, 0, 2f);


		public bool IsActive() => enableEffect.value;

		public bool IsTileCompatible() => false;
	}
}