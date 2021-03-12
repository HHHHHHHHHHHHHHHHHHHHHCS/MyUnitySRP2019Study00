using System.IO;
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


			if (GUILayout.Button("Update"))
			{
				weatherMap.UpdateMap();
				//在编辑模式下  强制更新
				EditorApplication.QueuePlayerLoopUpdate();
			}

			if (GUILayout.Button("Save"))
			{
				SaveTexture(weatherMap.weatherMap, textureName);
			}

			if (GUILayout.Button("Load"))
			{
				var t2d = Resources.Load<Texture2D>(textureName);
				weatherMap.weatherMap = new RenderTexture(t2d.width, t2d.height, 0);
				Graphics.Blit(t2d, weatherMap.weatherMap);
				EditorApplication.QueuePlayerLoopUpdate();
			}


			if (weatherMap.noiseSettings != null)
			{
				DrawSettingsEditor(weatherMap.noiseSettings, ref weatherMap.showSettingsEditor,
					ref noiseSettingsEditor);
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

		#region Save2DTexture

		private const string dirPath = "Assets/Res/Cloud/Resources";
		private const string textureName = "WeatherMap";


		public void SaveTexture(RenderTexture rt, string saveName)
		{
			var oldRT = RenderTexture.active;
			RenderTexture.active = rt;

			Texture2D weatherMap = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
			weatherMap.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			weatherMap.Apply();

			RenderTexture.active = oldRT;

			if (!Directory.Exists(dirPath))
			{
				Directory.CreateDirectory(dirPath);
			}

			// weatherMap.EncodeToTGA();
			var file = File.Create(dirPath + "/" + saveName + ".tga");
			var data = weatherMap.EncodeToTGA();
			file.Write(data, 0, data.Length);
			file.Dispose();
			AssetDatabase.Refresh();
			Debug.Log("Save : " + saveName);
		}

		Texture3D Tex3DFromTex2DArray(Texture2D[] slices, int resolution)
		{
			Texture3D tex3D = new Texture3D(resolution, resolution, resolution, TextureFormat.ARGB32, false);
			tex3D.filterMode = FilterMode.Trilinear;
			Color[] outputPixels = tex3D.GetPixels();

			for (int z = 0; z < resolution; z++)
			{
				// Color c = slices[z].GetPixel(0, 0);
				Color[] layerPixels = slices[z].GetPixels();
				for (int x = 0; x < resolution; x++)
				for (int y = 0; y < resolution; y++)
				{
					outputPixels[x + resolution * (y + z * resolution)] = layerPixels[x + y * resolution];
				}
			}

			tex3D.SetPixels(outputPixels);
			tex3D.Apply();

			return tex3D;
		}

		Texture2D ConvertFromRenderTexture(RenderTexture rt)
		{
			Texture2D output = new Texture2D(rt.width, rt.height);
			RenderTexture.active = rt;
			output.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
			output.Apply();
			return output;
		}

		#endregion
	}
}