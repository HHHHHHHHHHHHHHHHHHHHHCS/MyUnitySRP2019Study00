using System.Collections.Generic;
using UnityEngine;

namespace MyRenderPipeline.Utils
{
	public static class IdentifierPool
	{
		private static Queue<int> availableIDs = new Queue<int>();
		private static Dictionary<int, int> usedIDs = new Dictionary<int, int>();
		private static int nextID = 1;

		static IdentifierPool()
		{
		}


		public static int Get()
		{
			if (nextID >= 100)
			{
				Debug.LogWarning("RenderTextures might be leaking.");
			}

			if (availableIDs.Count <= 0)
			{
				availableIDs.Enqueue(nextID++);
			}

			var num = availableIDs.Dequeue();
			var id = Shader.PropertyToID($"RT_{num}");

			usedIDs[id] = num;
			return id;
		}

		public static void Release(int id)
		{
			if (usedIDs.ContainsKey(id))
			{
				var num = usedIDs[id];
				availableIDs.Enqueue(num);
			}
		}
	}
}