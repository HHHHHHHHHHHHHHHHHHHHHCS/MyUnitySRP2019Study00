#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


[ExecuteAlways]
public class CameraSync : MonoBehaviour
{
#if UNITY_EDITOR

	public bool syncCamera;

	private Transform sceneTs;
	private Transform gameTs;

	private void OnEnable()
	{
		gameTs = Camera.main.transform;
		//UnityEditor.SceneView.camera 也能获取sceneView.camera
		SceneView.duringSceneGui += OnCameraMove;
	}

	private void OnDisable()
	{
		SceneView.duringSceneGui -= OnCameraMove;
		sceneTs = null;
		gameTs = null;
	}

	private void OnCameraMove(SceneView sceneView)
	{
		if (!syncCamera)
		{
			return;
		}

		var cam = sceneView.camera;
		var camTs = cam.transform;

		if ((cam.cameraType & CameraType.SceneView) != 0
		    && camTs != sceneTs)
		{
			sceneTs = camTs;
		}

		//2020.2 Camera.main 优化了
#if UNITY_2020_2_OR_NEWER
			gameTs = Camera.main.transform;
#else
		if (gameTs == null)
		{
			gameTs = Camera.main.transform;
		}
#endif


		if (sceneTs != null && gameTs != null)
		{
			gameTs.position = sceneTs.position;
			gameTs.rotation = sceneTs.rotation;
		}
	}

#endif
}