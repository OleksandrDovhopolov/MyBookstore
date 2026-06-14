using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(Canvas))]
    public class WindowView : MonoBehaviour, IWindow
    {
        [SerializeField] private WindowAnimation _animation;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Canvas _canvas;

        public GameObject GameObject => gameObject;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : _rectTransform = GetComponent<RectTransform>();
        public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();
        public Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();
        public WindowAnimation Animation => _animation;
    }
}
