using DG.Tweening;
using UnityEngine;

namespace UIShared
{
    /// <summary>Lifts the tab along Y while it is selected.</summary>
    public sealed class MoveTabVisual : MonoBehaviour, ITabButtonVisual
    {
        [Tooltip("Child transform to lift. Leave empty to move the button root — only valid when no " +
                 "LayoutGroup owns this button, since a layout group rewrites child positions on every " +
                 "rebuild and would fight the tween.")]
        [SerializeField] private RectTransform _visualRoot;

        [SerializeField] private float _selectedYOffset = -50f;
        [SerializeField] private float _moveDuration = 0.18f;
        [SerializeField] private Ease _moveEase = Ease.OutCubic;

        private RectTransform _rectTransform;
        private Vector2 _basePosition;
        private Tween _moveTween;
        private bool _initialized;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _rectTransform = _visualRoot != null ? _visualRoot : transform as RectTransform;
            _basePosition = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
        }

        public void ApplySelected(bool selected)
        {
            EnsureInitialized();
            if (_rectTransform == null)
                return;

            var target = _basePosition;
            if (selected)
                target.y += _selectedYOffset;

            _moveTween?.Kill();
            _moveTween = null;

            if (_moveDuration <= 0f)
            {
                _rectTransform.anchoredPosition = target;
                return;
            }

            _moveTween = DOTween
                .To(() => _rectTransform.anchoredPosition, value => _rectTransform.anchoredPosition = value, target, _moveDuration)
                .SetEase(_moveEase)
                .SetUpdate(true);
        }

        private void OnDestroy()
        {
            _moveTween?.Kill();
            _moveTween = null;
        }
    }
}
