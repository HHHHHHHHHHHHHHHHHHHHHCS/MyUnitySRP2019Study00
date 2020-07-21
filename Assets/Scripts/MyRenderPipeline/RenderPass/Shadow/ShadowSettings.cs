using System;
using UnityEngine;

namespace MyRenderPipeline.Shadow
{
	public enum ShadowAlgorithms
	{
		Standard = 0,
		PSM = 1,
		TSM = 2,
		LiPsm = 3,
	}

	[RequireComponent(typeof(Light))]
	public class ShadowSettings : MonoBehaviour
	{
		public bool shadow = true;
		public ShadowAlgorithms algorithms = ShadowAlgorithms.Standard;
		[Delayed] public int resolution = 1024;
		public new Light light;
		public float maxShadowDistance = 50;
		public float bias = 0.01f;
		[Range(0,23)]
		public float depthBias = 1;
		[Range(0, 10)] public float normalBias = 1;
		public float nearDistance = 0.1f;
		public float focusDistance = 20f;
		public bool debug = false;

		private void Awake()
		{
			light = GetComponent<Light>();
		}

		private void Reset()
		{
			light = GetComponent<Light>();
		}

		private void Update()
		{
			resolution = Mathf.ClosestPowerOfTwo(resolution);
		}
	}
}