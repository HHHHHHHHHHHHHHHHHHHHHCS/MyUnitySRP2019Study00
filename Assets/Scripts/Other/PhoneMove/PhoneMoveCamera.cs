using System;
using UnityEngine;

namespace Other.PhoneMove
{
	public class PhoneMoveCamera : MonoBehaviour
	{
		public ScrollCircle rotateCom, moveCom;

		private Transform mainCamera;
		
		private void Awake()
		{
			mainCamera = Camera.main.transform;
			rotateCom.onValueChanged.AddListener(Rotate); 
			moveCom.onValueChanged.AddListener(Move); 
		}

		private void Rotate(Vector2 dir)
		{
			// mainCamera.transform.Rotate();
			// Debug.Log(dir.x);
		}

		private void Move(Vector2 dir)
		{
			
		}
	}
}
