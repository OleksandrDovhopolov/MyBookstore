using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    public class PointerHandlerHelper : MonoBehaviour, IPointerUpHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IDisposable
    {
        public Action<PointerEventData> ActionOnPointerUpData;
        public Action ActionOnPointerUp;

        public Action<PointerEventData> ActionOnPointerDown;

        public Action<PointerEventData> ActionOnBeginDragData;
        public Action ActionOnBeginDrag;

        public Action<PointerEventData> ActionOnEndDragData;
        public Action ActionOnEndDrag;

        public Action<PointerEventData> ActionOnPointerClickData;
        public Action ActionOnPointerClick;

        public Action<PointerEventData> ActionOnDrag;

        public Action ActionOnDisable;

        public void Dispose()
        {
            ActionOnPointerUpData = null;
            ActionOnPointerUp = null;

            ActionOnPointerDown = null;

            ActionOnBeginDragData = null;
            ActionOnBeginDrag = null;

            ActionOnEndDragData = null;
            ActionOnEndDrag = null;

            ActionOnPointerClickData = null;
            ActionOnPointerClick = null;

            ActionOnDrag = null;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ActionOnPointerUpData?.Invoke(eventData);
            ActionOnPointerUp?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ActionOnPointerDown?.Invoke(eventData);
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            ActionOnBeginDragData?.Invoke(eventData);
            ActionOnBeginDrag?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ActionOnEndDragData?.Invoke(eventData);
            ActionOnEndDrag?.Invoke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            ActionOnDrag?.Invoke(eventData);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            ActionOnPointerClickData?.Invoke(eventData);
            ActionOnPointerClick?.Invoke();
        }

        public void OnDisable()
        {
            ActionOnDisable?.Invoke();
        }
    }
}
