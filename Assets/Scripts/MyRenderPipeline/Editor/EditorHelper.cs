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
	class EditorHelper : UnityEditor.Editor
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
					customAttributeEditors[type.GetCustomAttribute<CustomAttributeEditorAttribute>().type] = type
				});
		}
	}
}