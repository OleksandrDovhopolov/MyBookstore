using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Infrastructure.ResourceAnimations
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ResourceParticleView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [Tooltip("Optional text-like component, e.g. TMP_Text. Must expose a writable string 'text' property.")]
        [SerializeField] private MonoBehaviour _countLabel;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private PropertyInfo _countTextProperty;

        public RectTransform RectTransform =>
            _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

        public CanvasGroup CanvasGroup =>
            _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        public void Bind(Sprite sprite, string countText)
        {
            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }

            SetCountText(countText ?? string.Empty);

            CanvasGroup.alpha = 1f;
        }

        public void Cleanup()
        {
            if (_icon != null)
            {
                _icon.sprite = null;
                _icon.enabled = false;
            }

            SetCountText(string.Empty);

            CanvasGroup.alpha = 1f;
            RectTransform.localScale = Vector3.one;
        }

        private void SetCountText(string text)
        {
            if (_countLabel == null) return;

            _countTextProperty ??= _countLabel.GetType().GetProperty(
                "text",
                BindingFlags.Instance | BindingFlags.Public);

            if (_countTextProperty == null ||
                !_countTextProperty.CanWrite ||
                _countTextProperty.PropertyType != typeof(string))
                return;

            _countTextProperty.SetValue(_countLabel, text);
        }
    }
}
