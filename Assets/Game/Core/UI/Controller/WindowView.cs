using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Canvas))]
    public class WindowView : MonoBehaviour, IWindow
    {
        public event Action CloseClick;

        [SerializeField] private WindowAnimation _animation;
        [SerializeField] private Button[] _closeButtons;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;

        public GameObject GameObject => gameObject;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : _rectTransform = GetComponent<RectTransform>();
        public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();
        public Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();
        public WindowAnimation Animation => _animation;

        protected virtual void Awake()
        {
            if (_closeButtons == null) return;

            foreach (var button in _closeButtons)
            {
                if (button == null) continue;
                button.onClick.AddListener(InvokeCloseEvent);
            }
        }

        protected virtual void OnDestroy()
        {
            if (_closeButtons == null) return;

            foreach (var button in _closeButtons)
            {
                if (button == null) continue;
                button.onClick.RemoveListener(InvokeCloseEvent);
            }
        }

        protected void InvokeCloseEvent() => CloseClick?.Invoke();
    }
}
