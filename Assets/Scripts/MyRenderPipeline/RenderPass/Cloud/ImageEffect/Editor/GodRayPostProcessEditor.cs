using MyRenderPipeline.RenderPass.GodRay;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect.Editor
{
	[VolumeComponentEditor(typeof(GodRayPostProcess))]
	public class GodRayPostProcessEditor : VolumeComponentEditor
	{
		
		private SerializedDataParameter m_enableEffect;
		private SerializedDataParameter m_godRayDir;
		private SerializedDataParameter m_godRayStrength;
		private SerializedDataParameter m_godRayMaxDistance;
		private SerializedDataParameter m_godRayColor;


		public override void OnEnable()
		{
			var o = new PropertyFetcher<GodRayPostProcess>(serializedObject);
			
			m_enableEffect = Unpack(o.Find(x => x.enableEffect));
			m_godRayDir = Unpack(o.Find(x => x.godRayDir));
			m_godRayStrength = Unpack(o.Find(x => x.godRayStrength));
			m_godRayMaxDistance = Unpack(o.Find(x => x.godRayMaxDistance));
			m_godRayColor = Unpack(o.Find(x => x.godRayColor));

		}

		public override void OnInspectorGUI()
		{
			// if (UniversalRenderPipeline.asset?.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
			// {
			// 	EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning,
			// 		MessageType.Warning);
			// 	return;
			// }

			EditorGUILayout.LabelField("GodRay", EditorStyles.miniLabel);

			PropertyField(m_enableEffect);
			PropertyField(m_godRayDir);
			PropertyField(m_godRayStrength);
			PropertyField(m_godRayMaxDistance);
			PropertyField(m_godRayColor);

		}
	}
}