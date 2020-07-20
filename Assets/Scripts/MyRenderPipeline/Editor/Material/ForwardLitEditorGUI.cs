using UnityEditor;
using UnityEngine;

namespace MyRenderPipeline.Editor.Material
{
	public class ForwardLitEditorGUI : ShaderGUI
	{
		const string MotionVectorPassName = "MotionVectors";

		public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
		{
			materialEditor.PropertiesDefaultGUI(properties);
return;
//TODO:
			foreach (var obj in materialEditor.targets)
			{
				var material = obj as UnityEngine.Material;
				material.SetShaderPassEnabled(MotionVectorPassName, false);
			}
		}
	}
}