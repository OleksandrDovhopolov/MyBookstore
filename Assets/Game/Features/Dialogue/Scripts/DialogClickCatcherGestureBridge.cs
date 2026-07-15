using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dialogue
{
    /// <summary>
    /// Lets the full-screen dialogue click catcher behave like a tap-to-advance surface and still forward
    /// drag gestures to the ScrollRect behind it.
    /// </summary>
    public sealed class DialogClickCatcherGestureBridge : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private ScrollRect _scrollRect;
        private Action _click;
        private bool _pointerDown;
        private bool _dragging;

        public void Configure(ScrollRect scrollRect, Action click)
        {
            _scrollRect = scrollRect;
            _click = click;
        }

        public void Clear()
        {
            _scrollRect = null;
            _click = null;
            _pointerDown = false;
            _dragging = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _dragging = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pointerDown && !_dragging)
                _click?.Invoke();

            _pointerDown = false;
            _dragging = false;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (_scrollRect != null)
                _scrollRect.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            if (_scrollRect != null)
                _scrollRect.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            _dragging = true;
            if (_scrollRect != null)
                _scrollRect.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_scrollRect != null)
                _scrollRect.OnEndDrag(eventData);

            _pointerDown = false;
        }
    }
}
