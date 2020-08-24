using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MoveCamera : MonoBehaviour
{
	public bool lockXRotation;
	public float minSpeed = 0.5f;
	public float mainSpeed = 10f; //正常的移动速度
	public float shiftMultiplier = 2f; //乘法移动速度 跑步用
	public float maxShift = 100000f; //最大的跑步乘法系数
	public float camSens = 0.35f; //相机输入的灵敏度
	public bool clickToMove = true;
	public bool isFrozen = false;

	private Vector3 lastMouse = new Vector3(Screen.width / 2, Screen.height / 2, 0);
	private float totalRun = 1.0f;

	private void Update()
	{
		if (isFrozen)
		{
			return;
		}

		mainSpeed += Input.GetAxis("Mouse ScrollWheel") * mainSpeed;
		if (mainSpeed < minSpeed)
		{
			mainSpeed = minSpeed;
		}

		if (clickToMove)
		{
			if (!Input.GetMouseButton(0))
			{
				return;
			}

			if (Input.GetMouseButtonDown(0))
			{
				lastMouse = Input.mousePosition;
				return;
			}
		}

		lastMouse = Input.mousePosition - lastMouse;
		lastMouse = new Vector3(-lastMouse.y * camSens, lastMouse.x * camSens, 0);
		lastMouse = new Vector3(transform.eulerAngles.x + lastMouse.x, transform.eulerAngles.y + lastMouse.y, 0);
		transform.eulerAngles = lastMouse;
		lastMouse = Input.mousePosition;

		Vector3 p = GetDirection();
		if (Input.GetKey(KeyCode.LeftShift))
		{
			totalRun += Time.unscaledDeltaTime;
			p = p * totalRun * shiftMultiplier;
			p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
			p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
			p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
		}
		else
		{
			totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
			p = p * mainSpeed;
		}

		p = p * Time.unscaledDeltaTime;
		Vector3 newPosition = transform.position;
		if (Input.GetKey(KeyCode.V))
		{
			transform.Translate(p);
			newPosition.x = transform.position.x;
			newPosition.z = transform.position.z;
			transform.position = newPosition;
		}
		else
		{
			transform.Translate(p);
		}

		if (lockXRotation)
		{
			Vector3 euler = transform.localRotation.eulerAngles;
			euler.x = 0f;
			transform.localRotation = Quaternion.Euler(euler);
		}
	}


	private Vector3 GetDirection()
	{
		Vector3 p_Velocity = new Vector3();
		if (Input.GetKey(KeyCode.W))
		{
			p_Velocity += new Vector3(0, 0, 1);
		}

		if (Input.GetKey(KeyCode.S))
		{
			p_Velocity += new Vector3(0, 0, -1);
		}

		if (Input.GetKey(KeyCode.A))
		{
			p_Velocity += new Vector3(-1, 0, 0);
		}

		if (Input.GetKey(KeyCode.D))
		{
			p_Velocity += new Vector3(1, 0, 0);
		}

		if (Input.GetKey(KeyCode.R))
		{
			p_Velocity += new Vector3(0, 1, 0);
		}

		if (Input.GetKey(KeyCode.F))
		{
			p_Velocity += new Vector3(0, -1, 0);
		}

		return p_Velocity;
	}
}