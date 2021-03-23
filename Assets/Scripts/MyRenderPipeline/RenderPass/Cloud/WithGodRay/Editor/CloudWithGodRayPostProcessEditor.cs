using UnityEditor;
using UnityEditor.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud.WithGodRay.Editor
{
	[VolumeComponentEditor(typeof(CloudWithGodRayPostProcess))]
	public class CloudWithGodRayPostProcessEditor : VolumeComponentEditor
	{
		private SerializedDataParameter m_enableEffect;
		private SerializedDataParameter m_shapeTiling;
		private SerializedDataParameter m_detailTiling;
		private SerializedDataParameter m_colA;
		private SerializedDataParameter m_colB;
		private SerializedDataParameter m_colorOffset1;
		private SerializedDataParameter m_colorOffset2;
		private SerializedDataParameter m_lightAbsorptionTowardSun;
		private SerializedDataParameter m_lightAbsorptionThroughCloud;
		private SerializedDataParameter m_phaseParams;
		private SerializedDataParameter m_densityOffset;
		private SerializedDataParameter m_densityMultiplier;
		private SerializedDataParameter m_step;
		private SerializedDataParameter m_rayStep;
		private SerializedDataParameter m_rayOffsetStrength;
		private SerializedDataParameter m_downsample;
		private SerializedDataParameter m_heightWeights;
		private SerializedDataParameter m_shapeNoiseWeights;
		private SerializedDataParameter m_detailWeights;
		private SerializedDataParameter m_detailNoiseWeight;
		private SerializedDataParameter m_detailNoiseWeights;
		private SerializedDataParameter m_xy_Speed_zw_Warp;

		public override void OnEnable()
		{
			var o = new PropertyFetcher<CloudWithGodRayPostProcess>(serializedObject);

			m_enableEffect = Unpack(o.Find(x => x.enableEffect));
			m_shapeTiling = Unpack(o.Find(x => x.shapeTiling));
			m_detailTiling = Unpack(o.Find(x => x.detailTiling));
			m_colA = Unpack(o.Find(x => x.colA));
			m_colB = Unpack(o.Find(x => x.colB));
			m_colorOffset1 = Unpack(o.Find(x => x.colorOffset1));
			m_colorOffset2 = Unpack(o.Find(x => x.colorOffset2));
			m_lightAbsorptionTowardSun = Unpack(o.Find(x => x.lightAbsorptionTowardSun));
			m_lightAbsorptionThroughCloud = Unpack(o.Find(x => x.lightAbsorptionThroughCloud));
			m_phaseParams = Unpack(o.Find(x => x.phaseParams));
			m_densityOffset = Unpack(o.Find(x => x.densityOffset));
			m_densityMultiplier = Unpack(o.Find(x => x.densityMultiplier));
			m_step = Unpack(o.Find(x => x.step));
			m_rayStep = Unpack(o.Find(x => x.rayStep));
			m_rayOffsetStrength = Unpack(o.Find(x => x.rayOffsetStrength));
			m_downsample = Unpack(o.Find(x => x.downsample));
			m_heightWeights = Unpack(o.Find(x => x.heightWeights));
			m_shapeNoiseWeights = Unpack(o.Find(x => x.shapeNoiseWeights));
			m_detailWeights = Unpack(o.Find(x => x.detailWeights));
			m_detailNoiseWeight = Unpack(o.Find(x => x.detailNoiseWeight));
			m_detailNoiseWeights = Unpack(o.Find(x => x.detailNoiseWeights));
			m_xy_Speed_zw_Warp = Unpack(o.Find(x => x.xy_Speed_zw_Warp));
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
			PropertyField(m_shapeTiling);
			PropertyField(m_detailTiling);
			PropertyField(m_colA);
			PropertyField(m_colB);
			PropertyField(m_colorOffset1);
			PropertyField(m_colorOffset2);
			PropertyField(m_lightAbsorptionTowardSun);
			PropertyField(m_lightAbsorptionThroughCloud);
			PropertyField(m_phaseParams);
			PropertyField(m_densityOffset);
			PropertyField(m_densityMultiplier);
			PropertyField(m_step);
			PropertyField(m_rayStep);
			PropertyField(m_rayOffsetStrength);
			PropertyField(m_downsample);
			PropertyField(m_heightWeights);
			PropertyField(m_shapeNoiseWeights);
			PropertyField(m_detailWeights);
			PropertyField(m_detailNoiseWeight);
			PropertyField(m_detailNoiseWeights);
			PropertyField(m_xy_Speed_zw_Warp);
		}
	}
}