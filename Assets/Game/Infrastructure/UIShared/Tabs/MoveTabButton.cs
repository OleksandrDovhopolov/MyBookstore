using DG.Tweening;
using UnityEngine;

namespace UIShared
{
    public sealed class MoveTabButton : TabButton
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

        protected override void Awake()
        {
            base.Awake();
            _rectTransform = _visualRoot != null ? _visualRoot : transform as RectTransform;
            _basePosition = _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero;
        }

        protected override void ApplySelected(bool selected)
        {
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

        protected override void OnDestroy()
        {
            _moveTween?.Kill();
            _moveTween = null;
            base.OnDestroy();
        }
    }
}
