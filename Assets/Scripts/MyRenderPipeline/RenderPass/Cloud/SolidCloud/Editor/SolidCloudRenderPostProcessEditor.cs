using MyRenderPipeline.RenderPass.Cloud.WithGodRay;
using UnityEditor;
using UnityEditor.Rendering;

namespace MyRenderPipeline.RenderPass.Cloud.SolidCloud.Editor
{
	[VolumeComponentEditor(typeof(SolidCloudRenderPostProcess))]
	public class SolidCloudRenderPostProcessEditor : VolumeComponentEditor
	{
		private SerializedDataParameter m_enableEffect;
		private SerializedDataParameter m_height;
		private SerializedDataParameter m_density;
		private SerializedDataParameter m_noiseScale;
		private SerializedDataParameter m_distance;
		private SerializedDataParameter m_distanceFallOff;
		private SerializedDataParameter m_maxLength;
		private SerializedDataParameter m_maxLengthFallOff;
		private SerializedDataParameter m_windDirection;
		private SerializedDataParameter m_windSpeed;
		private SerializedDataParameter m_noiseSpeed;
		private SerializedDataParameter m_cloudAlbedoColor;
		private SerializedDataParameter m_cloudSpecularColor;
		private SerializedDataParameter m_cloudAreaPosition;
		private SerializedDataParameter m_cloudAreaRadius;
		private SerializedDataParameter m_cloudAreaHeight;
		private SerializedDataParameter m_cloudAreaDepth;
		private SerializedDataParameter m_cloudAreaFallOff;
		private SerializedDataParameter m_sunShadowsStrength;
		private SerializedDataParameter m_sunShadowsJitterStrength;
		private SerializedDataParameter m_sunShadowsCancellation;
		private SerializedDataParameter m_stepping;
		private SerializedDataParameter m_steppingNear;
		private SerializedDataParameter m_ditherStrength;


		public override void OnEnable()
		{
			var o = new PropertyFetcher<SolidCloudRenderPostProcess>(serializedObject);

			m_enableEffect = Unpack(o.Find(x => x.enableEffect));
			m_height = Unpack(o.Find(x => x.height));
			m_density = Unpack(o.Find(x => x.density));
			m_noiseScale = Unpack(o.Find(x => x.noiseScale));
			m_distance = Unpack(o.Find(x => x.distance));
			m_distanceFallOff = Unpack(o.Find(x => x.distanceFallOff));
			m_maxLength = Unpack(o.Find(x => x.maxLength));
			m_maxLengthFallOff = Unpack(o.Find(x => x.maxLengthFallOff));
			m_windDirection = Unpack(o.Find(x => x.windDirection));
			m_windSpeed = Unpack(o.Find(x => x.windSpeed));
			m_noiseSpeed = Unpack(o.Find(x => x.noiseSpeed));
			m_cloudAlbedoColor = Unpack(o.Find(x => x.cloudAlbedoColor));
			m_cloudSpecularColor = Unpack(o.Find(x => x.cloudSpecularColor));
			m_cloudAreaPosition = Unpack(o.Find(x => x.cloudAreaPosition));
			m_cloudAreaRadius = Unpack(o.Find(x => x.cloudAreaRadius));
			m_cloudAreaHeight = Unpack(o.Find(x => x.cloudAreaHeight));
			m_cloudAreaDepth = Unpack(o.Find(x => x.cloudAreaDepth));
			m_cloudAreaFallOff = Unpack(o.Find(x => x.cloudAreaFallOff));
			m_sunShadowsStrength = Unpack(o.Find(x => x.sunShadowsStrength));
			m_sunShadowsJitterStrength = Unpack(o.Find(x => x.sunShadowsJitterStrength));
			m_sunShadowsCancellation = Unpack(o.Find(x => x.sunShadowsCancellation));
			m_stepping = Unpack(o.Find(x => x.stepping));
			m_steppingNear = Unpack(o.Find(x => x.steppingNear));
			m_ditherStrength = Unpack(o.Find(x => x.ditherStrength));
		}


		public override void OnInspectorGUI()
		{
			// if (UniversalRenderPipeline.asset?.postProcessingFeatureSet == PostProcessingFeatureSet.PostProcessingV2)
			// {
			// 	EditorGUILayout.HelpBox(UniversalRenderPipelineAssetEditor.Styles.postProcessingGlobalWarning,
			// 		MessageType.Warning);
			// 	return;
			// }

			EditorGUILayout.LabelField("SolidCloud", EditorStyles.miniLabel);

			PropertyField(m_enableEffect);
			PropertyField(m_height);
			PropertyField(m_density);
			PropertyField(m_noiseScale);
			PropertyField(m_distance);
			PropertyField(m_distanceFallOff);
			PropertyField(m_maxLength);
			PropertyField(m_maxLengthFallOff);
			PropertyField(m_windDirection);
			PropertyField(m_windSpeed);
			PropertyField(m_noiseSpeed);
			PropertyField(m_cloudAlbedoColor);
			PropertyField(m_cloudSpecularColor);
			PropertyField(m_cloudAreaPosition);
			PropertyField(m_cloudAreaRadius);
			PropertyField(m_cloudAreaHeight);
			PropertyField(m_cloudAreaDepth);
			PropertyField(m_cloudAreaFallOff);
			PropertyField(m_sunShadowsStrength);
			PropertyField(m_sunShadowsJitterStrength);
			PropertyField(m_sunShadowsCancellation);
			PropertyField(m_stepping);
			PropertyField(m_steppingNear);
			PropertyField(m_ditherStrength);
		}
	}
}