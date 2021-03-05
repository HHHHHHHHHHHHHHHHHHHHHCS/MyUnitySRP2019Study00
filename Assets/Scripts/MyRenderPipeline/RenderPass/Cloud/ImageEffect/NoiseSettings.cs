using System;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public abstract class NoiseSettings : ScriptableObject
	{
		public event System.Action onValueChanged;

		public abstract System.Array GetDataArray();

		public abstract int Stride { get; }

		private void OnValidate()
		{
			onValueChanged?.Invoke();
		}
	}
}