using System.Collections.Generic;
using UnityEngine;

namespace MyRenderPipeline.RenderPass.GrassCulling
{
	[ExecuteAlways]
	public class GrassCullingPosDefine : MonoBehaviour
	{
		[Range(1, 40000000)] public int instanceCount = 1000000;
		public float drawDistance = 125;

		private int cacheCount = -1;

		private void Start()
		{
			UpdatePosIfNeeded();
		}

		private void Update()
		{
			UpdatePosIfNeeded();
		}

		private void OnGUI()
		{
			GUI.Label(new Rect(300, 50, 200, 30), "Instance Count: " + instanceCount / 1000000 + "Million");
			instanceCount = Mathf.Max(1,
				(int) (GUI.HorizontalSlider(new Rect(300, 100, 200, 30), instanceCount / 1000000f, 1, 10)) * 1000000);

			GUI.Label(new Rect(300, 150, 200, 30), "Draw Distance: " + drawDistance);
			drawDistance = Mathf.Max(1,
				(int) (GUI.HorizontalSlider(new Rect(300, 200, 200, 30), drawDistance / 25f, 1, 8)) * 25);
		}

		private void UpdatePosIfNeeded()
		{
			if (GrassCullingRenderer.instance.drawDistance != drawDistance)
			{
				GrassCullingRenderer.instance.drawDistance = drawDistance;
			}

			if (instanceCount == cacheCount)
				return;

			Debug.Log("UpdatePos (Slow)");

			//same seed to keep grass visual the same
			Random.InitState(123);

			//保持密度不怎么变化
			float scale = Mathf.Sqrt((instanceCount / 4)) / 2f;
			transform.localScale = new Vector3(scale, transform.localScale.y, scale);


			List<Vector3> positions = new List<Vector3>(instanceCount);
			for (int i = 0; i < instanceCount; i++)
			{
				Vector3 pos = Vector3.zero;

				pos.x = Random.Range(-1f, 1f) * transform.lossyScale.x;
				pos.z = Random.Range(-1f, 1f) * transform.lossyScale.z;

				//transform to posWS in C#
				pos += transform.position;

				positions.Add(pos);
			}

			//send all posWS to renderer
			GrassCullingRenderer.instance.allGrassPos = positions;
			cacheCount = positions.Count;
		}
	}
}