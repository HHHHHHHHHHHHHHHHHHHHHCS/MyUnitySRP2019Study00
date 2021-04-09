using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FPSRate : MonoBehaviour
{
	public int fpsRate = 1000;

	private void Awake()
	{
		Application.targetFrameRate = fpsRate;
	}
}
