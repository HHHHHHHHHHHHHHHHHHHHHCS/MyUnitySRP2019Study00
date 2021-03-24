using MyRenderPipeline.RenderPass.Cloud.WithGodRay;
using UnityEditor;
using UnityEditor.Rendering;

namespace MyRenderPipeline.RenderPass.CrepuscularRay.Editor
{
	[VolumeComponentEditor(typeof(CrepuscularRayPostProcess))]
	public class CrepuscularRayPostProcessEditor : VolumeComponentEditor
	{
		public SerializedDataParameter m_enableEffect;
		public SerializedDataParameter m_qualityMode;
		public SerializedDataParameter m_lightColor;
		public SerializedDataParameter m_rayRange;
		public SerializedDataParameter m_rayIntensity;
		public SerializedDataParameter m_rayPower;
		public SerializedDataParameter m_lightThreshold;
		public SerializedDataParameter m_qualityStep;
		public SerializedDataParameter m_offsetUV;
		public SerializedDataParameter m_boxBlur;
		public SerializedDataParameter m_downsample;


		public override void OnEnable()
		{
			var o = new PropertyFetcher<CrepuscularRayPostProcess>(serializedObject);

			m_enableEffect = Unpack(o.Find(x => x.enableEffect));
			m_qualityMode = Unpack(o.Find(x => x.qualityMode));
			m_lightColor = Unpack(o.Find(x => x.lightColor));
			m_rayRange = Unpack(o.Find(x => x.rayRange));
			m_rayIntensity = Unpack(o.Find(x => x.rayIntensity));
			m_rayPower = Unpack(o.Find(x => x.rayPower));
			m_lightThreshold = Unpack(o.Find(x => x.lightThreshold));
			m_qualityStep = Unpack(o.Find(x => x.qualityStep));
			m_offsetUV = Unpack(o.Find(x => x.offsetUV));
			m_boxBlur = Unpack(o.Find(x => x.boxBlur));
			m_downsample = Unpack(o.Find(x => x.downsample));
		}


		public override void OnInspectorGUI()
		{
			// if (UniversalRenderPipeline.asset?.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
			// {
			// 	EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning,
			// 		MessageType.Warning);
			// 	return;
			// }

			EditorGUILayout.LabelField("CrepuscularRay", EditorStyles.miniLabel);

			PropertyField(m_enableEffect);
			PropertyField(m_qualityMode);
			PropertyField(m_lightColor);
			PropertyField(m_rayRange);
			PropertyField(m_rayIntensity);
			PropertyField(m_rayPower);
			PropertyField(m_lightThreshold);

			if (m_qualityMode.value.intValue == (int) CrepuscularRayPostProcess.QualityMode.customQuality)
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("CustomQuality", EditorStyles.miniLabel);
				PropertyField(m_qualityStep);
				PropertyField(m_offsetUV);
				PropertyField(m_boxBlur);
				PropertyField(m_downsample);
			}
		}
	}
}