using System;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.Cloud.ImageEffect
{
	public class ContainerVis : MonoBehaviour
	{
		// public static ContainerVis Instance { get; private set; }
		//
		// private void Awake()
		// {
		// 	Instance = this;
		// }
		//
		// private void OnDestroy()
		// {
		// 	if (Instance == this)
		// 	{
		// 		Instance = null;
		// 	}
		// }


#if UNITY_EDITOR
		public Color color = Color.green;
		public bool displayOutline = true;

		private void OnDrawGizmosSelected()
		{
			if (displayOutline)
			{
				Gizmos.color = color;
				Gizmos.DrawWireCube(transform.position, transform.lossyScale);
			}
		}
#endif
	}
}