using System.Reflection;
using MyRenderPipeline.Utility;
using UnityEngine;

namespace MyRenderPipeline.Editor
{
	[CustomAttributeEditor(typeof(EditorButtonAttribute))]
	public class ButtonEditor : AttributeEditor
	{
		public override void OnEdit(MemberInfo member, CustomEditorAttribute attr)
		{
			var method = member as MethodInfo;
			var buttonAttr = attr as EditorButtonAttribute;

			if (method is null)
				return;

			var label = buttonAttr.Label == "" ? member.Name : buttonAttr.Label;
			if (GUILayout.Button(label))
				method.Invoke(target, null);
		}
	}
}