using System;
using System.IO;
using System.Text;
using MyRenderPipeline.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect.Editor
{
	[CustomEditor(typeof(NoiseGenerator))]
	public class NoiseGeneratorEditor : UnityEditor.Editor
	{
		private NoiseGenerator noise;
		private UnityEditor.Editor noiseSettingsEditor;
		private ComputeShader slicerCS;


		private void OnEnable()
		{
			noise = (NoiseGenerator) target;
		}

		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			slicerCS = noise.slicerCS;

			if (GUILayout.Button("Update"))
			{
				noise.ManualUpdate();
				//在编辑模式下  强制更新
				EditorApplication.QueuePlayerLoopUpdate();
			}

			if (GUILayout.Button("Save"))
			{
				Save();
			}

			if (GUILayout.Button("Load"))
			{
				Load();
			}


			if (noise.ActiveSettings != null)
			{
				DrawSettingsEditor(noise.ActiveSettings, ref noise.showSettingsEditor, ref noiseSettingsEditor);
			}
		}

		void Save()
		{
			if (noise.activeTextureType == NoiseGenerator.CloudNoiseType.Shape)
			{
				Save(noise.shapeTexture, NoiseGenerator.shapeNoiseName);
			}
			else
			{
				Save(noise.detailTexture, NoiseGenerator.detailNoiseName);
			}
		}

		void Load()
		{
			noise.Load(NoiseGenerator.shapeNoiseName, noise.shapeTexture);
			noise.Load(NoiseGenerator.detailNoiseName, noise.detailTexture);
			EditorApplication.QueuePlayerLoopUpdate();
		}

		void DrawSettingsEditor(Object settings, ref bool foldout, ref UnityEditor.Editor editor)
		{
			if (settings != null)
			{
				foldout = EditorGUILayout.InspectorTitlebar(foldout, settings);
				using (var check = new EditorGUI.ChangeCheckScope())
				{
					if (foldout)
					{
						//创建editor的缓存   如果editor!=null  则直接返回
						CreateCachedEditor(settings, null, ref editor);
						editor.OnInspectorGUI();
					}

					if (check.changed)
					{
						noise.ActiveNoiseSettingsChanged();
					}
				}
			}
		}

		#region Save3DTexture

		private const int threadGroupSize = 32;
		private const string dirPath = "Assets/Cloud/Res/Resources";

		public void Save(RenderTexture volumeTexture, string saveName)
		{
			if (slicerCS == null)
			{
				Debug.LogError("SlicerCS is null!");
				return;
			}

			string sceneName = SceneManager.GetActiveScene().name;
			saveName = sceneName + "_" + saveName;
			int resolution = volumeTexture.width;
			Texture2D[] slices = new Texture2D[resolution];

			slicerCS.SetInt("resolution", resolution);
			slicerCS.SetTexture(0, "volumeTexture", volumeTexture);

			// StringBuilder sb = new StringBuilder();

			for (int layer = 0; layer < resolution; layer++)
			{
				var slice = new RenderTexture(resolution, resolution, 0);
				slice.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
				slice.enableRandomWrite = true;
				slice.Create();

				slicerCS.SetTexture(0, "slice", slice);
				slicerCS.SetInt("layer", layer);
				int numThreadGroups = Mathf.CeilToInt(resolution / (float) threadGroupSize);
				slicerCS.Dispatch(0, numThreadGroups, numThreadGroups, 1);

				slices[layer] = ConvertFromRenderTexture(slice);

				// sb.Clear();
				// foreach (var item in slices[layer].GetPixels())
				// {
				// 	sb.Append($"({item.r}.{item.g},{item.b},{item.a})").Append("|");
				// }
				// Debug.Log(sb);				
			}

			var x = Tex3DFromTex2DArray(slices, resolution);

			if (!Directory.Exists(dirPath))
			{
				Directory.CreateDirectory(dirPath);
			}

			AssetDatabase.CreateAsset(x, dirPath + "/" + saveName + ".asset");
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