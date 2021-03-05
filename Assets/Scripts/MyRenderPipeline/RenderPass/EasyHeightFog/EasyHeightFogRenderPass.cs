using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.RenderPass.EasyHeightFog
{
	public class EasyHeightFogRenderPass : ScriptableRenderPass
	{
		private const float UNUSE_VALUE = 0.0f;

		private static readonly int exponentialFogParameters_ID = Shader.PropertyToID("_ExponentialFogParameters");
		private static readonly int exponentialFogParameters2_ID = Shader.PropertyToID("_ExponentialFogParameters2");
		private static readonly int exponentialFogParameters3_ID = Shader.PropertyToID("_ExponentialFogParameters3");

		private static readonly int directionalInscatteringColor_ID =
			Shader.PropertyToID("_DirectionalInscatteringColor");

		private static readonly int inscatteringLightDirection_ID = Shader.PropertyToID("_InscatteringLightDirection");

		private static readonly int exponentialFogColorParameter_ID =
			Shader.PropertyToID("_ExponentialFogColorParameter");

		private Material easyHeightFogMaterial;


		public EasyHeightFogRenderPass(Material _material)
		{
			easyHeightFogMaterial = _material;
		}

		private static float RayOriginTerm(float density, float heightFalloff, float heightOffset)
		{
			float exponent = heightFalloff * (Camera.main.transform.position.y - heightOffset);
			return density * Mathf.Pow(2.0f, -exponent);
		}

		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			var settings = VolumeManager.instance.stack.GetComponent<EasyHeightFogPostProcess>();
			if (settings == null || !settings.IsActive())
			{
				return;
			}


			var exponentialFogParameters =
				new Vector4(
					RayOriginTerm(settings.fogDensity.value, settings.fogHeightFalloff.value, settings.fogHeight.value),
					settings.fogHeightFalloff.value, UNUSE_VALUE, settings.startDistance.value);

			var exponentialFogParameters2 =
				new Vector4(
					RayOriginTerm(settings.fogDensity2.value, settings.fogHeightFalloff2.value,
						settings.fogHeight2.value),
					settings.fogHeightFalloff2.value, settings.fogDensity2.value, settings.fogHeight2.value);

			var exponentialFogParameters3 = new Vector4(settings.fogDensity.value, settings.fogHeight.value,
				UNUSE_VALUE, settings.fogCutoffDistance.value);

			var directionalInscatteringColor = new Vector4(
				settings.directionalInscatteringIntensity.value * settings.directionalInscatteringColor.value.r,
				settings.directionalInscatteringIntensity.value * settings.directionalInscatteringColor.value.g,
				settings.directionalInscatteringIntensity.value * settings.directionalInscatteringColor.value.b,
				settings.directionalInscatteringExponent.value
			);

			Vector3 mainLightDir = Vector3.forward;
			var mainLightIndex = renderingData.lightData.mainLightIndex;
			if (mainLightIndex >= 0)
			{
				mainLightDir = renderingData.lightData.visibleLights[mainLightIndex].light.transform.forward;
			}

			var inscatteringLightDirection = new Vector4(
				-mainLightDir.x,
				-mainLightDir.y,
				-mainLightDir.z,
				settings.directionalInscatteringStartDistance.value
			);
			var exponentialFogColorParameter = new Vector4(
				settings.fogInscatteringColor.value.r,
				settings.fogInscatteringColor.value.g,
				settings.fogInscatteringColor.value.b,
				1.0f - settings.fogMaxOpacity.value
			);

			easyHeightFogMaterial.SetVector(exponentialFogParameters_ID, exponentialFogParameters);
			easyHeightFogMaterial.SetVector(exponentialFogParameters2_ID, exponentialFogParameters2);
			easyHeightFogMaterial.SetVector(exponentialFogParameters3_ID, exponentialFogParameters3);
			easyHeightFogMaterial.SetVector(directionalInscatteringColor_ID, directionalInscatteringColor);
			easyHeightFogMaterial.SetVector(inscatteringLightDirection_ID, inscatteringLightDirection);
			easyHeightFogMaterial.SetVector(exponentialFogColorParameter_ID, exponentialFogColorParameter);
		}
	}
}