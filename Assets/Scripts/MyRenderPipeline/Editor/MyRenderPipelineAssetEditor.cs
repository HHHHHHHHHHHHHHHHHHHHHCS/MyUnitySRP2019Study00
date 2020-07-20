using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MyRenderPipeline.Editor
{
	[CustomEditor(typeof(MyRenderPipelineAsset))]
	public class MyRenderPipelineAssetEditor : UnityEditor.Editor
	{
		private UnityEditorInternal.ReorderableList reorderable;

		private void OnEnable()
		{
			reorderable = new ReorderableList(serializedObject, serializedObject.FindProperty("m_RenderPasses"));
			reorderable.drawHeaderCallback =
				(rect) =>
				{
					EditorGUI.LabelField(rect, "Render Pass");
				};
			reorderable.drawElementCallback =
				(rect, index, isActive, isFocus) =>
				{
					var obj = reorderable.serializedProperty.GetArrayElementAtIndex(index);
					rect.y += (rect.height - EditorGUIUtility.singleLineHeight) / 4; //加一个空的间隙
					EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), obj,
						GUIContent.none);
				};
		}
		
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();
			serializedObject.Update();
			reorderable.DoLayoutList();
			serializedObject.ApplyModifiedProperties();
		}
	}
}