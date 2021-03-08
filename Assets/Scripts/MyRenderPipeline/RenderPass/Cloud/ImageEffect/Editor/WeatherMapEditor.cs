using UnityEditor;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect.Editor
{
	[CustomEditor(typeof(WeatherMap))]
	public class WeatherMapEditor : UnityEditor.Editor
	{
		private WeatherMap weatherMap;
		private UnityEditor.Editor noiseSettingsEditor;
		private bool showSettingsEditor;

		private void OnEnable()
		{
			weatherMap = (WeatherMap) target;
		}

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			if (weatherMap.noiseSettings != null)
			{
				DrawSettingsEditor(weatherMap.noiseSettings, ref weatherMap.showSettingsEditor, ref noiseSettingsEditor);
			}

		}

		private void DrawSettingsEditor(Object settings, ref bool foldout, ref UnityEditor.Editor editor)
		{
			if (settings != null)
			{
				foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
				using (var check = new EditorGUI.ChangeCheckScope())
				{
					if (foldout)
					{
						CreateCachedEditor(settings, null, ref editor);
						editor.OnInspectorGUI();
					}
				}
			}
		}
	}
}