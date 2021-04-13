using System;
using UnityEngine;
using UnityEngine.UI;

namespace Other.PhoneMove
{
	public class ScrollCircle : ScrollRect
	{
		public Action<Vector2> onDragEvent;
		
		protected float radius = 0f;

		protected override void Start()
		{
			base.Start();
			//计算摇杆块的半径
			radius = ((RectTransform) transform).sizeDelta.x * 0.5f;
		}

		public override void OnDrag(UnityEngine.EventSystems.PointerEventData eventData)
		{
			base.OnDrag(eventData);
			content.anchoredPosition = Vector3.ClampMagnitude(base.content.anchoredPosition , this.radius);

			onDragEvent?.Invoke(content.anchoredPosition / (((RectTransform) transform).sizeDelta/2.0f));
		}
	}
}