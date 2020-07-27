using System;

namespace MyRenderPipeline.Utility
{
	public abstract class CustomEditorAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
	public class EditorButtonAttribute : CustomEditorAttribute
	{
		public string Label { get; private set; }

		public EditorButtonAttribute(string label = "")
		{
			Label = label;
		}
	}
}