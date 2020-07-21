using System.Collections.Generic;
using UnityEngine;

namespace MyRenderPipeline.Utils
{
	public class ShaderPool
	{
		private static Dictionary<string, Material> pool = new Dictionary<string, Material>();

		public static Material Get(string name)
		{
			if (!pool.ContainsKey(name) || !pool[name])
				pool[name] = new Material(Shader.Find(name));
			return pool[name];
		}
	}
}