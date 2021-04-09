using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateLight : MonoBehaviour
{
	public Light mainLight;
	public Light[] pointLight;

	public float YRotateSpeed = 45;

	private bool pointEnable;

	// Update is called once per frame
	void Update()
	{
		if (mainLight == null)
		{
			return;
		}

		mainLight.transform.Rotate(Vector3.up, YRotateSpeed * Time.deltaTime, Space.World);
	}

	private void OnGUI()
	{
		if (mainLight == null || pointLight == null)
		{
			return;
		}

		mainLight.enabled = GUI.Toggle(new Rect(100, 150, 200, 30), mainLight.enabled, "Main Light On/OFF");
		bool _pointEnable = GUI.Toggle(new Rect(100, 250, 200, 30), pointEnable, "PointLight Light On/OFF");

		if (_pointEnable != pointEnable)
		{
			pointEnable = _pointEnable;
			foreach (var item in pointLight)
			{
				item.enabled = pointEnable;
			}
		}


		GUI.Label(new Rect(100, 350, 200, 30), "Light Rotate Speed");
		YRotateSpeed = (int) (GUI.HorizontalSlider(new Rect(100, 400, 200, 30), YRotateSpeed, 0, 90));
	}
}