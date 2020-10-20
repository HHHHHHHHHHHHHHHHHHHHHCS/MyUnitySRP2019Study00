using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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
		SceneView.duringSceneGui += OnCameraMove;
	}

	private void OnDestroy()
	{
		SceneView.duringSceneGui -= OnCameraMove;
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

		if (gameTs == null)
		{
			gameTs = Camera.main.transform;
		}

		if (sceneTs != null && gameTs != null)
		{
			gameTs.position = sceneTs.position;
			gameTs.rotation = sceneTs.rotation;
		}
	}

#endif
}