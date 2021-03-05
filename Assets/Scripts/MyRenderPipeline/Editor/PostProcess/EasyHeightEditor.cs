using MyRenderPipeline.RenderPass.EasyHeightFog;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyRenderPipeline.Editor.PostProcess
{
	[VolumeComponentEditor(typeof(EasyHeightFogPostProcess))]
	public class EasyHeightEditor : VolumeComponentEditor
	{
		private SerializedDataParameter m_fogDensity;
		private SerializedDataParameter m_fogHeightFalloff;
		private SerializedDataParameter m_fogHeight;
		private SerializedDataParameter m_fogDensity2;
		private SerializedDataParameter m_fogHeightFalloff2;
		private SerializedDataParameter m_fogHeight2;
		private SerializedDataParameter m_fogInscatteringColor;
		private SerializedDataParameter m_fogMaxOpacity;
		private SerializedDataParameter m_startDistance;
		private SerializedDataParameter m_fogCutoffDistance;
		private SerializedDataParameter m_directionalInscatteringExponent;
		private SerializedDataParameter m_directionalInscatteringStartDistance;
		private SerializedDataParameter m_directionalInscatteringColor;
		private SerializedDataParameter m_directionalInscatteringIntensity;


		public override void OnEnable()
		{
			var o = new PropertyFetcher<EasyHeightFogPostProcess>(serializedObject);

			m_fogDensity = Unpack(o.Find(x => x.fogDensity));
			m_fogHeightFalloff = Unpack(o.Find(x => x.fogHeightFalloff));
			m_fogHeight = Unpack(o.Find(x => x.fogHeight));
			m_fogDensity2 = Unpack(o.Find(x => x.fogDensity2));
			m_fogHeightFalloff2 = Unpack(o.Find(x => x.fogHeightFalloff2));
			m_fogHeight2 = Unpack(o.Find(x => x.fogHeight2));
			m_fogInscatteringColor = Unpack(o.Find(x => x.fogInscatteringColor));
			m_fogMaxOpacity = Unpack(o.Find(x => x.fogMaxOpacity));
			m_startDistance = Unpack(o.Find(x => x.startDistance));
			m_fogCutoffDistance = Unpack(o.Find(x => x.fogCutoffDistance));
			m_directionalInscatteringExponent = Unpack(o.Find(x => x.directionalInscatteringExponent));
			m_directionalInscatteringStartDistance = Unpack(o.Find(x => x.directionalInscatteringStartDistance));
			m_directionalInscatteringColor = Unpack(o.Find(x => x.directionalInscatteringColor));
			m_directionalInscatteringIntensity = Unpack(o.Find(x => x.directionalInscatteringIntensity));
		}

		public override void OnInspectorGUI()
		{
			// if (UniversalRenderPipeline.asset?.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
			// {
			// 	EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning,
			// 		MessageType.Warning);
			// 	return;
			// }

			EditorGUILayout.LabelField("EasyHeightFog", EditorStyles.miniLabel);

			PropertyField(m_fogDensity);
			PropertyField(m_fogHeightFalloff);
			PropertyField(m_fogHeight);
			PropertyField(m_fogDensity2);
			PropertyField(m_fogHeightFalloff2);
			PropertyField(m_fogHeight2);
			PropertyField(m_fogInscatteringColor);
			PropertyField(m_fogMaxOpacity);
			PropertyField(m_startDistance);
			PropertyField(m_fogCutoffDistance);
			PropertyField(m_directionalInscatteringExponent);
			PropertyField(m_directionalInscatteringStartDistance);
			PropertyField(m_directionalInscatteringColor);
			PropertyField(m_directionalInscatteringIntensity);
		}
	}
}