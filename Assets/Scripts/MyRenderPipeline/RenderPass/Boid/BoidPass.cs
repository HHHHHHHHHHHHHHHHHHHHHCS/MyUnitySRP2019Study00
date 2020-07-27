using UnityEngine;

namespace MyRenderPipeline.RenderPass.Boid
{
	[ImageEffectAllowedInSceneView]
	public class BoidPass : MyUserPass
	{
		struct EntityData
		{
			public Vector3 position;
			public Vector3 velocity;
			public Vector3 up;
			public Matrix4x4 rotation;
			public static int Size => sizeof(float) * 3 * 3 + sizeof(float) * 4 * 4;
		}


		public Transform spawnPoint;
		
		
		private void Awake()
		{
			spawnPoint = transform;
		}
		
		[EditorButton]
		public void Reload()
		{
			FindObjectsOfType<BoidRenderer>().ForEach(renderer =>
			{
				renderer.needUpdate = true;
			});
			UnityEditor.SceneView.GetAllSceneCameras().Select(camera => camera.GetComponent<BoidRenderer>()).ForEach(renderer =>
			{
				renderer.needUpdate = true;
			});
		}
	}
}