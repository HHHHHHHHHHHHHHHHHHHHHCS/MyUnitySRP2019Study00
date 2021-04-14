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
			rotateCom.onDragEvent += Rotate;
			moveCom.onDragEvent += Move;
		}

		private void Rotate(Vector2 dir)
		{
			mainCamera.Rotate(-dir.y, dir.x, 0);
		}

		private void Move(Vector2 dir)
		{
			mainCamera.Translate(dir.x, 0, dir.y);
		}
	}
}