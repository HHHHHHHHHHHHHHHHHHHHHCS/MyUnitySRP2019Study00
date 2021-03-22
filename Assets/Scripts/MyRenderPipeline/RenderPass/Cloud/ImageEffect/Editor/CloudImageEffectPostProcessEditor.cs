using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect.Editor
{
	[VolumeComponentEditor(typeof(CloudImageEffectPostProcess))]
	public class CloudImageEffectPostProcessEditor : VolumeComponentEditor
	{
		
		private SerializedDataParameter m_enableEffect;
		private SerializedDataParameter m_followCamera;
		private SerializedDataParameter m_useSkybox;
		private SerializedDataParameter m_cloudTestParams;
		private SerializedDataParameter m_numStepsLight;
		private SerializedDataParameter m_rayOffsetStrength;
		private SerializedDataParameter m_cloudScale;
		private SerializedDataParameter m_densityMultiplier;
		private SerializedDataParameter m_densityOffset;
		private SerializedDataParameter m_shapeOffset;
		private SerializedDataParameter m_heightOffset;
		private SerializedDataParameter m_shapeNoiseWeights;
		private SerializedDataParameter m_detailNoiseScale;
		private SerializedDataParameter m_detailNoiseWeight;
		private SerializedDataParameter m_detailNoiseWeights;
		private SerializedDataParameter m_detailOffset;
		private SerializedDataParameter m_lightAbsorptionThroughCloud;
		private SerializedDataParameter m_lightAbsorptionTowardSun;
		private SerializedDataParameter m_darknessThreshold;
		private SerializedDataParameter m_forwardScattering;
		private SerializedDataParameter m_backScattering;
		private SerializedDataParameter m_baseBrightness;
		private SerializedDataParameter m_phaseFactor;
		private SerializedDataParameter m_timeScale;
		private SerializedDataParameter m_baseSpeed;
		private SerializedDataParameter m_detailSpeed;
		private SerializedDataParameter m_colA;
		private SerializedDataParameter m_colB;
		private SerializedDataParameter m_debugMode;
		private SerializedDataParameter m_viewerShadowAllChannels;
		private SerializedDataParameter m_viewerColorMask;
		private SerializedDataParameter m_viewerGreyScale;
		private SerializedDataParameter m_viewerSliceDepth;
		private SerializedDataParameter m_viewerTileAmount;
		private SerializedDataParameter m_viewerSize;

		public override void OnEnable()
		{
			var o = new PropertyFetcher<CloudImageEffectPostProcess>(serializedObject);
			
			m_enableEffect = Unpack(o.Find(x => x.enableEffect));
			m_followCamera = Unpack(o.Find(x => x.followCamera));
			m_useSkybox = Unpack(o.Find(x => x.useSkybox));
			m_cloudTestParams = Unpack(o.Find(x => x.cloudTestParams));
			m_numStepsLight = Unpack(o.Find(x => x.numStepsLight));
			m_rayOffsetStrength = Unpack(o.Find(x => x.rayOffsetStrength));
			m_cloudScale = Unpack(o.Find(x => x.cloudScale));
			m_densityMultiplier = Unpack(o.Find(x => x.densityMultiplier));
			m_densityOffset = Unpack(o.Find(x => x.densityOffset));
			m_shapeOffset = Unpack(o.Find(x => x.shapeOffset));
			m_heightOffset = Unpack(o.Find(x => x.heightOffset));
			m_shapeNoiseWeights = Unpack(o.Find(x => x.shapeNoiseWeights));
			m_detailNoiseScale = Unpack(o.Find(x => x.detailNoiseScale));
			m_detailNoiseWeight = Unpack(o.Find(x => x.detailNoiseWeight));
			m_detailNoiseWeights = Unpack(o.Find(x => x.detailNoiseWeights));
			m_detailOffset = Unpack(o.Find(x => x.detailOffset));
			m_lightAbsorptionThroughCloud = Unpack(o.Find(x => x.lightAbsorptionThroughCloud));
			m_lightAbsorptionTowardSun = Unpack(o.Find(x => x.lightAbsorptionTowardSun));
			m_darknessThreshold = Unpack(o.Find(x => x.darknessThreshold));
			m_forwardScattering = Unpack(o.Find(x => x.forwardScattering));
			m_backScattering = Unpack(o.Find(x => x.backScattering));
			m_baseBrightness = Unpack(o.Find(x => x.baseBrightness));
			m_phaseFactor = Unpack(o.Find(x => x.phaseFactor));
			m_timeScale = Unpack(o.Find(x => x.timeScale));
			m_baseSpeed = Unpack(o.Find(x => x.baseSpeed));
			m_detailSpeed = Unpack(o.Find(x => x.detailSpeed));
			m_colA = Unpack(o.Find(x => x.colA));
			m_colB = Unpack(o.Find(x => x.colB));
			m_debugMode = Unpack(o.Find(x => x.debugMode));
			m_viewerShadowAllChannels = Unpack(o.Find(x => x.viewerShadowAllChannels));
			m_viewerColorMask = Unpack(o.Find(x => x.viewerColorMask));
			m_viewerGreyScale = Unpack(o.Find(x => x.viewerGreyScale));
			m_viewerSliceDepth = Unpack(o.Find(x => x.viewerSliceDepth));
			m_viewerTileAmount = Unpack(o.Find(x => x.viewerTileAmount));
			m_viewerSize = Unpack(o.Find(x => x.viewerSize));
		}

		public override void OnInspectorGUI()
		{
			// if (UniversalRenderPipeline.asset?.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
			// {
			// 	EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning,
			// 		MessageType.Warning);
			// 	return;
			// }

			EditorGUILayout.LabelField("CloudImageEffect", EditorStyles.miniLabel);

			PropertyField(m_enableEffect);
			PropertyField(m_followCamera);
			PropertyField(m_useSkybox);
			PropertyField(m_cloudTestParams);
			PropertyField(m_numStepsLight);
			PropertyField(m_rayOffsetStrength);
			PropertyField(m_cloudScale);
			PropertyField(m_densityMultiplier);
			PropertyField(m_densityOffset);
			PropertyField(m_shapeOffset);
			PropertyField(m_heightOffset);
			PropertyField(m_shapeNoiseWeights);
			PropertyField(m_detailNoiseScale);
			PropertyField(m_detailNoiseWeight);
			PropertyField(m_detailNoiseWeights);
			PropertyField(m_detailOffset);
			PropertyField(m_lightAbsorptionThroughCloud);
			PropertyField(m_lightAbsorptionTowardSun);
			PropertyField(m_darknessThreshold);
			PropertyField(m_forwardScattering);
			PropertyField(m_backScattering);
			PropertyField(m_baseBrightness);
			PropertyField(m_phaseFactor);
			PropertyField(m_timeScale);
			PropertyField(m_baseSpeed);
			PropertyField(m_detailSpeed);
			PropertyField(m_colA);
			PropertyField(m_colB);
			PropertyField(m_debugMode);
			PropertyField(m_viewerShadowAllChannels);
			PropertyField(m_viewerColorMask);
			PropertyField(m_viewerGreyScale);
			PropertyField(m_viewerSliceDepth);
			PropertyField(m_viewerTileAmount);
			PropertyField(m_viewerSize);
		}
		
		
	}
}