using System;
using DG.Tweening;
using UnityEngine;

namespace UIShared
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class AnimatedShowHidePanel : MonoBehaviour
    {
        public enum PanelState
        {
            Shown,
            Hidden
        }

        public enum SlideDirection
        {
            Up,
            Down,
            Left,
            Right
        }

        [SerializeField, Min(0f)] private float _duration = 0.25f;
        [SerializeField] private SlideDirection _slideDirection = SlideDirection.Up;
        [SerializeField, Min(0f)] private float _slideDistance = 60f;
        [SerializeField] private Ease _ease = Ease.OutQuad;
        [SerializeField] private bool _startShown = true;

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Sequence _activeTween;
        private Vector2 _shownAnchoredPos;
        private bool _initialized;

        public PanelState CurrentState { get; private set; } = PanelState.Shown;

        private RectTransform RectTransform =>
            _rectTransform != null ? _rectTransform : _rectTransform = (RectTransform)transform;

        private CanvasGroup CanvasGroup =>
            _canvasGroup != null ? _canvasGroup : _canvasGroup = GetComponent<CanvasGroup>();

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            _shownAnchoredPos = RectTransform.anchoredPosition;
            CurrentState = _startShown ? PanelState.Shown : PanelState.Hidden;
            ApplyInstant(CurrentState);
        }

        public void Show(bool instant = false, Action onComplete = null)
        {
            EnsureInitialized();
            SetState(PanelState.Shown, instant, onComplete);
        }

        public void Hide(bool instant = false, Action onComplete = null)
        {
            EnsureInitialized();
            SetState(PanelState.Hidden, instant, onComplete);
        }

        private void SetState(PanelState target, bool instant, Action onComplete)
        {
            CurrentState = target;
            KillActiveTween();

            if (instant || _duration <= 0f)
            {
                ApplyInstant(target);
                onComplete?.Invoke();
                return;
            }

            var targetAlpha = target == PanelState.Shown ? 1f : 0f;
            var targetPos = TargetPosition(target);

            CanvasGroup.interactable = target == PanelState.Shown;
            CanvasGroup.blocksRaycasts = target == PanelState.Shown;

            var alphaTween = DOTween.To(
                () => CanvasGroup.alpha,
                x => CanvasGroup.alpha = x,
                targetAlpha,
                _duration);

            var posTween = DOTween.To(
                () => RectTransform.anchoredPosition,
                p => RectTransform.anchoredPosition = p,
                targetPos,
                _duration);

            _activeTween = DOTween.Sequence()
                .Join(alphaTween.SetEase(_ease))
                .Join(posTween.SetEase(_ease))
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() => onComplete?.Invoke());
        }

        private void ApplyInstant(PanelState target)
        {
            var alpha = target == PanelState.Shown ? 1f : 0f;

            CanvasGroup.alpha = alpha;
            CanvasGroup.interactable = target == PanelState.Shown;
            CanvasGroup.blocksRaycasts = target == PanelState.Shown;

            RectTransform.anchoredPosition = TargetPosition(target);
        }

        private Vector2 TargetPosition(PanelState target) =>
            target == PanelState.Shown ? _shownAnchoredPos : _shownAnchoredPos + HiddenOffset();

        private Vector2 HiddenOffset()
        {
            switch (_slideDirection)
            {
                case SlideDirection.Up: return new Vector2(0f, _slideDistance);
                case SlideDirection.Down: return new Vector2(0f, -_slideDistance);
                case SlideDirection.Left: return new Vector2(-_slideDistance, 0f);
                case SlideDirection.Right: return new Vector2(_slideDistance, 0f);
                default: return Vector2.zero;
            }
        }

        private void KillActiveTween()
        {
            if (_activeTween == null || !_activeTween.IsActive()) return;
            _activeTween.Kill(false);
            _activeTween = null;
        }

        private void OnDestroy() => KillActiveTween();
    }
}
