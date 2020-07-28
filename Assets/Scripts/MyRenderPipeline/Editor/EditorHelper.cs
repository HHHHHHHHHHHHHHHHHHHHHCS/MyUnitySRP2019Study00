using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MyRenderPipeline.Utility;
using UnityEditor;
using UnityEngine;

namespace MyRenderPipeline.Editor
{
	[CustomEditor(typeof(UnityEngine.Object), true), CanEditMultipleObjects]
	public class EditorHelper : UnityEditor.Editor
	{
		private Dictionary<Type, AttributeEditor> attributeEditorInstances = new Dictionary<Type, AttributeEditor>();
		private MemberInfo[] members;
		private CustomEditorAttribute[] attrs;

		public EditorHelper()
		{
			if (!CustomEditorHelper.loaded)
			{
				CustomEditorHelper.Reload();
			}
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (members == null)
			{
				members = target.GetType()
					.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					.Where(member => member.MemberType == MemberTypes.Field
					                 || member.MemberType == MemberTypes.Property ||
					                 (member.MemberType == MemberTypes.Method && !(member as MethodInfo).IsSpecialName))
					.Where(member => member.GetCustomAttribute<CustomEditorAttribute>(true) != null)
					.ToArray();
				attrs = members.Select(member => member.GetCustomAttribute<CustomEditorAttribute>(true)).ToArray();
			}

			for (int i = 0; i < members.Length; i++)
			{
				var member = members[i];

				var attr = attrs[i];
				if (attr is null)
					continue;
				var attrType = attr.GetType();
				if (!attributeEditorInstances.ContainsKey(attrType))
				{
					var editorType = CustomEditorHelper.customAttributeEditors[attrType];
					if (editorType is null)
						continue;

					var editor = Activator.CreateInstance(editorType) as AttributeEditor;
					editor.target = target;
					attributeEditorInstances[attrType] = editor;
				}

				attributeEditorInstances[attrType].OnEdit(member, attr);
			}
		}
	}


	public abstract class AttributeEditor
	{
		public UnityEngine.Object target;
		public abstract void OnEdit(MemberInfo member, CustomEditorAttribute attr);
	}


	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public class CustomAttributeEditorAttribute : Attribute
	{
		public Type type { get; private set; }

		public CustomAttributeEditorAttribute(Type type) : base()
		{
			this.type = type;
		}
	}

	public static class CustomEditorHelper
	{
		public static bool loaded = false;
		public static Dictionary<Type, Type> customAttributeEditors = new Dictionary<Type, Type>();

		public static void Reload()
		{
			loaded = true;
			typeof(CustomEditorHelper).Assembly.GetTypes()
				.Where(type => type.IsSubclassOf(typeof(AttributeEditor)))
				.Where(type => type.GetCustomAttribute<CustomAttributeEditorAttribute>() != null)
				.ForEach(type =>
				{
					customAttributeEditors[type.GetCustomAttribute<CustomAttributeEditorAttribute>().type] = type;
				});
		}
	}
}