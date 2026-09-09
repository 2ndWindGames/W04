using System;
using SWGUnity2DCore.Manager;
using SWGUnity2DCore.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SWGUnity2DCore.UI
{
	public class UI_EventHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
	{
		public Action OnClickHandler = null;
		public Action OnPressedHandler = null;
		public Action OnPointerDownHandler = null;
		public Action OnPointerUpHandler = null;

		bool _pressed = false;

		private void Update()
		{
			if (_pressed)
				OnPressedHandler?.Invoke();
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (GetComponent<Button>() != null)
				CoreServices.ButtonClicked?.Invoke();

			OnClickHandler?.Invoke();
		}

		public void OnPointerDown(PointerEventData eventData)
		{
			_pressed = true;
			OnPointerDownHandler?.Invoke();
		}

		public void OnPointerUp(PointerEventData eventData)
		{
			_pressed = false;
			OnPointerUpHandler?.Invoke();
		}
	}
}
