using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.UI;
using UIShared;
using UnityEngine;

namespace Book.Sell.UI
{
    public sealed class RecommendationMinigameWindowAnimation : WindowAnimation
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup _windowCanvasGroup;
        [SerializeField] private RectTransform _minigameRoot;

        [Header("Panels")]
        [SerializeField] private RectTransform _header;
        [SerializeField] private RectTransform _bookDetailedRoot;
        [SerializeField] private RectTransform _booksBackground;
        [SerializeField] private AnimatedShowHidePanel _booksBackgroundPanel;
        [SerializeField] private RectTransform _requestRoot;

        [Header("Timings")]
        [SerializeField, Min(0f)] private float _duration = 0.5f;
        [SerializeField, Min(0f)] private float _rootFadeDuration = 0.2f;
        [SerializeField, Min(0f)] private float _hideRootFadeDuration = 0.5f;
        [SerializeField, Min(0f)] private float _panelStagger = 0.04f;
        [SerializeField, Min(0f)] private float _requestOvershootDuration = 0.16f;

        [Header("Motion")]
        [SerializeField, Min(0f)] private float _upSlideDistance = 500f;
        [SerializeField, Min(0f)] private float _downSlideDistance = 500f;
        [SerializeField, Min(0.01f)] private float _requestScaleOvershoot = 1.15f;
        [SerializeField] private Ease _showEase = Ease.OutBack;
        [SerializeField] private Ease _hideEase = Ease.InBack;
        [SerializeField] private Ease _fadeEase = Ease.OutQuad;

        private Sequence _activeTween;
        private PanelSnapshot _headerSnapshot;
        private PanelSnapshot _bookDetailedSnapshot;
        private PanelSnapshot _booksSnapshot;
        private PanelSnapshot _requestSnapshot;
        private bool _captured;

        public override float DefaultDuration => _duration;

        private void Awake() => CaptureShownLayout();

        public override async UniTask PlayInAsync(CancellationToken ct)
        {
            CaptureShownLayout();
            KillActiveTween(complete: false);
            SetRootInteractable(false);
            SetRootAlpha(0f);
            ApplyHiddenForShow();

            var sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this);

            InsertRootFade(sequence, targetAlpha: 1f, _rootFadeDuration);
            InsertPanelShow(sequence, _headerSnapshot, start: 0f);
            InsertPanelShow(sequence, _bookDetailedSnapshot, start: _panelStagger);
            InsertRequestShow(sequence, start: _panelStagger);

            _activeTween = sequence;
            var booksTask = PlayBooksBackgroundAsync(show: true, ct);

            await UniTask.WhenAll(AwaitTweenAsync(_activeTween, ct), booksTask);
            if (ct.IsCancellationRequested) return;

            ApplyShown();
            SetRootInteractable(true);
        }

        public override async UniTask PlayOutAsync(CancellationToken ct)
        {
            CaptureShownLayout();
            KillActiveTween(complete: false);
            SetRootInteractable(false);

            var sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this);

            InsertRootFade(sequence, targetAlpha: 0f, _hideRootFadeDuration);
            InsertPanelHide(sequence, _headerSnapshot, Direction.Up, start: 0f);
            InsertPanelHide(sequence, _bookDetailedSnapshot, Direction.Up, start: _panelStagger);
            InsertRequestHide(sequence, start: 0f);

            _activeTween = sequence;
            var booksTask = PlayBooksBackgroundAsync(show: false, ct);

            await UniTask.WhenAll(AwaitTweenAsync(_activeTween, ct), booksTask);
            if (ct.IsCancellationRequested) return;

            SetRootAlpha(0f);
        }

        private void CaptureShownLayout()
        {
            if (_captured) return;
            _captured = true;

            _headerSnapshot = PanelSnapshot.Capture(_header);
            _bookDetailedSnapshot = PanelSnapshot.Capture(_bookDetailedRoot);
            _booksSnapshot = PanelSnapshot.Capture(_booksBackground);
            _requestSnapshot = PanelSnapshot.Capture(_requestRoot);
        }

        private void ApplyHiddenForShow()
        {
            ApplyPanelHidden(_headerSnapshot, Direction.Up);
            ApplyPanelHidden(_bookDetailedSnapshot, Direction.Up);
            if (_booksBackgroundPanel != null) _booksBackgroundPanel.Hide(instant: true);
            else ApplyPanelHidden(_booksSnapshot, Direction.Down);
            ApplyRequestHidden();
        }

        private void ApplyShown()
        {
            ApplyPanelShown(_headerSnapshot);
            ApplyPanelShown(_bookDetailedSnapshot);
            if (_booksBackgroundPanel == null) ApplyPanelShown(_booksSnapshot);
            ApplyPanelShown(_requestSnapshot);
        }

        private void InsertPanelShow(Sequence sequence, PanelSnapshot snapshot, float start)
        {
            if (!snapshot.IsValid) return;

            var duration = Mathf.Max(0.01f, _duration - start);
            sequence.Insert(start, DOTween.To(
                    () => snapshot.Group.alpha,
                    x => snapshot.Group.alpha = x,
                    1f,
                    duration)
                .SetEase(_fadeEase));
            sequence.Insert(start, DOTween.To(
                    () => snapshot.Rect.anchoredPosition,
                    x => snapshot.Rect.anchoredPosition = x,
                    snapshot.Position,
                    duration)
                .SetEase(_showEase));
        }

        private void InsertRootFade(Sequence sequence, float targetAlpha, float duration)
        {
            if (_windowCanvasGroup == null) return;

            sequence.Insert(0f, DOTween.To(
                    () => _windowCanvasGroup.alpha,
                    x => _windowCanvasGroup.alpha = x,
                    targetAlpha,
                    Mathf.Max(0.01f, duration))
                .SetEase(_fadeEase));
        }

        private void InsertPanelHide(Sequence sequence, PanelSnapshot snapshot, Direction direction, float start)
        {
            if (!snapshot.IsValid) return;

            var duration = Mathf.Max(0.01f, _duration - start);
            sequence.Insert(start, DOTween.To(
                    () => snapshot.Group.alpha,
                    x => snapshot.Group.alpha = x,
                    0f,
                    duration)
                .SetEase(Ease.InQuad));
            sequence.Insert(start, DOTween.To(
                    () => snapshot.Rect.anchoredPosition,
                    x => snapshot.Rect.anchoredPosition = x,
                    HiddenPosition(snapshot, direction),
                    duration)
                .SetEase(_hideEase));
        }

        private void InsertRequestShow(Sequence sequence, float start)
        {
            if (!_requestSnapshot.IsValid) return;

            var growDuration = Mathf.Max(0.01f, _duration - start - _requestOvershootDuration);
            var settleDuration = Mathf.Max(0.01f, _requestOvershootDuration);
            var overshoot = _requestSnapshot.Scale * _requestScaleOvershoot;

            sequence.Insert(start, DOTween.To(
                    () => _requestSnapshot.Group.alpha,
                    x => _requestSnapshot.Group.alpha = x,
                    1f,
                    growDuration)
                .SetEase(_fadeEase));
            sequence.Insert(start, DOTween.To(
                    () => _requestSnapshot.Rect.localScale,
                    x => _requestSnapshot.Rect.localScale = x,
                    overshoot,
                    growDuration)
                .SetEase(Ease.OutCubic));
            sequence.Insert(start + growDuration, DOTween.To(
                    () => _requestSnapshot.Rect.localScale,
                    x => _requestSnapshot.Rect.localScale = x,
                    _requestSnapshot.Scale,
                    settleDuration)
                .SetEase(Ease.OutBack));
        }

        private void InsertRequestHide(Sequence sequence, float start)
        {
            if (!_requestSnapshot.IsValid) return;

            var punchDuration = Mathf.Max(0.01f, _requestOvershootDuration);
            var collapseDuration = Mathf.Max(0.01f, _duration - punchDuration);
            var overshoot = _requestSnapshot.Scale * _requestScaleOvershoot;

            sequence.Insert(start, DOTween.To(
                    () => _requestSnapshot.Rect.localScale,
                    x => _requestSnapshot.Rect.localScale = x,
                    overshoot,
                    punchDuration)
                .SetEase(Ease.OutCubic));
            sequence.Insert(start + punchDuration, DOTween.To(
                    () => _requestSnapshot.Rect.localScale,
                    x => _requestSnapshot.Rect.localScale = x,
                    Vector3.zero,
                    collapseDuration)
                .SetEase(Ease.InBack));
            sequence.Insert(start + punchDuration, DOTween.To(
                    () => _requestSnapshot.Group.alpha,
                    x => _requestSnapshot.Group.alpha = x,
                    0f,
                    collapseDuration)
                .SetEase(Ease.InQuad));
        }

        private UniTask PlayBooksBackgroundAsync(bool show, CancellationToken ct)
        {
            if (_booksBackgroundPanel != null)
                return PlayPanelAsync(_booksBackgroundPanel, show, ct);

            if (_booksSnapshot.IsValid)
            {
                var sequence = DOTween.Sequence()
                    .SetUpdate(true)
                    .SetTarget(this);
                if (show) InsertPanelShow(sequence, _booksSnapshot, start: _panelStagger * 2f);
                else InsertPanelHide(sequence, _booksSnapshot, Direction.Down, start: _panelStagger * 2f);
                return AwaitTweenAsync(sequence, ct);
            }

            return UniTask.CompletedTask;
        }

        private static async UniTask PlayPanelAsync(AnimatedShowHidePanel panel, bool show, CancellationToken ct)
        {
            if (panel == null) return;

            var tcs = new UniTaskCompletionSource();
            if (show) panel.Show(instant: false, () => tcs.TrySetResult());
            else panel.Hide(instant: false, () => tcs.TrySetResult());

            using (ct.Register(() => tcs.TrySetResult()))
                await tcs.Task;
        }

        private void ApplyPanelHidden(PanelSnapshot snapshot, Direction direction)
        {
            if (!snapshot.IsValid) return;
            snapshot.Rect.anchoredPosition = HiddenPosition(snapshot, direction);
            snapshot.Rect.localScale = snapshot.Scale;
            snapshot.Group.alpha = 0f;
            snapshot.Group.interactable = false;
            snapshot.Group.blocksRaycasts = false;
        }

        private void ApplyRequestHidden()
        {
            if (!_requestSnapshot.IsValid) return;
            _requestSnapshot.Rect.anchoredPosition = _requestSnapshot.Position;
            _requestSnapshot.Rect.localScale = Vector3.zero;
            _requestSnapshot.Group.alpha = 0f;
            _requestSnapshot.Group.interactable = false;
            _requestSnapshot.Group.blocksRaycasts = false;
        }

        private static void ApplyPanelShown(PanelSnapshot snapshot)
        {
            if (!snapshot.IsValid) return;
            snapshot.Rect.anchoredPosition = snapshot.Position;
            snapshot.Rect.localScale = snapshot.Scale;
            snapshot.Group.alpha = 1f;
            snapshot.Group.interactable = true;
            snapshot.Group.blocksRaycasts = true;
        }

        private Vector2 HiddenPosition(PanelSnapshot snapshot, Direction direction)
        {
            var distance = direction == Direction.Up ? _upSlideDistance : _downSlideDistance;
            var offset = direction == Direction.Up
                ? new Vector2(0f, distance)
                : new Vector2(0f, -distance);
            return snapshot.Position + offset;
        }

        private void SetRootAlpha(float alpha)
        {
            if (_windowCanvasGroup != null) _windowCanvasGroup.alpha = alpha;
        }

        private void SetRootInteractable(bool interactable)
        {
            SetInteractable(_windowCanvasGroup, interactable);
            if (_minigameRoot != null)
                SetInteractable(EnsureCanvasGroup(_minigameRoot.gameObject), interactable);
        }

        private static void SetInteractable(CanvasGroup group, bool interactable)
        {
            if (group == null) return;
            group.interactable = interactable;
            group.blocksRaycasts = interactable;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null) return null;
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        private async UniTask AwaitTweenAsync(Tween tween, CancellationToken ct)
        {
            if (tween == null) return;

            var tcs = new UniTaskCompletionSource();
            tween.OnComplete(() => tcs.TrySetResult());
            tween.OnKill(() => tcs.TrySetResult());
            using (ct.Register(() => tcs.TrySetResult()))
                await tcs.Task;
        }

        private void KillActiveTween(bool complete)
        {
            if (_activeTween != null && _activeTween.IsActive()) _activeTween.Kill(complete);
            _activeTween = null;
        }

        private void OnDestroy() => KillActiveTween(complete: false);

        private enum Direction
        {
            Up,
            Down
        }

        [Serializable]
        private struct PanelSnapshot
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Vector2 Position;
            public Vector3 Scale;

            public bool IsValid => Rect != null && Group != null;

            public static PanelSnapshot Capture(RectTransform rect)
            {
                return new PanelSnapshot
                {
                    Rect = rect,
                    Group = rect != null ? EnsureCanvasGroup(rect.gameObject) : null,
                    Position = rect != null ? rect.anchoredPosition : Vector2.zero,
                    Scale = rect != null ? rect.localScale : Vector3.one
                };
            }
        }
    }
}
