using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.WorldHud;
using UnityEngine;

namespace Book.Sell.UI.Customer
{
    [WorldHud("WorldHud/CustomerThoughtBubble")]
    [RequireComponent(typeof(CustomerThoughtBubbleView))]
    public sealed class CustomerThoughtBubble : WorldHud
    {
        private const float CrossfadeDuration = 0.2f;
        private const float ScaleInDuration = 0.15f;
        private static readonly Vector3 ScaleInFrom = Vector3.one * 0.5f;
        private static readonly Vector3 ScaleInTo = Vector3.one;

        private CustomerThoughtBubbleView _view;
        private CanvasGroup _currentActive;
        private CancellationTokenSource _stateCts;

        public CustomerThoughtState State { get; private set; } = CustomerThoughtState.None;

        protected override void OnAttached()
        {
            _view = GetComponent<CustomerThoughtBubbleView>();
            DeactivateAll();

            if (CanvasGroup != null) CanvasGroup.alpha = 1f;
        }

        protected override async UniTask OnDetachAsync(CancellationToken ct)
        {
            _stateCts?.Cancel();
            _stateCts = null;

            if (CanvasGroup != null)
            {
                await TweenAsync.LerpAlphaAsync(CanvasGroup, CanvasGroup.alpha, 0f, CrossfadeDuration, ct);
            }
        }

        public async UniTask SetStateAsync(
            CustomerThoughtState state,
            CustomerThoughtPayload payload = null,
            CancellationToken externalCt = default)
        {
            payload ??= CustomerThoughtPayload.Empty;

            _stateCts?.Cancel();
            _stateCts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _stateCts.Token;

            try
            {
                ApplyContent(state, payload);

                var next = ResolveGroup(state);
                await CrossfadeAsync(_currentActive, next, ct);

                // Per-state secondary animation (subtle scale-in for book / rejection).
                await PlaySecondaryAsync(state, ct);

                State = state;
            }
            catch (OperationCanceledException) { /* superseded — swallow */ }
        }

        private void ApplyContent(CustomerThoughtState state, CustomerThoughtPayload payload)
        {
            if (_view == null) return;

            switch (state)
            {
                case CustomerThoughtState.BookPicked:
                    if (_view.BookIcon != null) _view.BookIcon.sprite = payload.BookSprite;
                    break;
                case CustomerThoughtState.Comment:
                    if (_view.CommentText != null) _view.CommentText.text = payload.CommentText ?? string.Empty;
                    break;
                case CustomerThoughtState.Rejected:
                    if (_view.RejectedBookIcon != null) _view.RejectedBookIcon.sprite = payload.RejectedBookSprite;
                    if (_view.ReplacementBookIcon != null) _view.ReplacementBookIcon.sprite = payload.ReplacementBookSprite;
                    break;
            }
        }

        private CanvasGroup ResolveGroup(CustomerThoughtState state) => state switch
        {
            CustomerThoughtState.Thinking => _view?.DotsGroup,
            CustomerThoughtState.ThinkingNext => _view?.DotsGroup,
            CustomerThoughtState.BookPicked => _view?.BookGroup,
            CustomerThoughtState.Comment => _view?.CommentGroup,
            CustomerThoughtState.Rejected => _view?.RejectionGroup,
            _ => null,
        };

        private async UniTask CrossfadeAsync(CanvasGroup from, CanvasGroup to, CancellationToken ct)
        {
            if (from == to)
            {
                if (to != null) to.alpha = 1f;
                return;
            }

            // Fade out the current sub-view (if any), then fade in the new one. Could be parallelized,
            // but sequential is more legible and the duration is short.
            if (from != null)
            {
                await TweenAsync.LerpAlphaAsync(from, from.alpha, 0f, CrossfadeDuration, ct);
                from.gameObject.SetActive(false);
            }

            if (to != null)
            {
                to.gameObject.SetActive(true);
                to.alpha = 0f;
                await TweenAsync.LerpAlphaAsync(to, 0f, 1f, CrossfadeDuration, ct);
            }

            _currentActive = to;
        }

        private async UniTask PlaySecondaryAsync(CustomerThoughtState state, CancellationToken ct)
        {
            if (_view == null) return;

            switch (state)
            {
                case CustomerThoughtState.BookPicked when _view.BookScaleTarget != null:
                    await TweenAsync.LerpScaleAsync(_view.BookScaleTarget, ScaleInFrom, ScaleInTo, ScaleInDuration, ct);
                    break;
                case CustomerThoughtState.Rejected when _view.RejectionScaleTarget != null:
                    await TweenAsync.LerpScaleAsync(_view.RejectionScaleTarget, ScaleInFrom, ScaleInTo, ScaleInDuration, ct);
                    break;
            }
        }

        private void DeactivateAll()
        {
            if (_view == null) return;
            if (_view.DotsGroup != null) { _view.DotsGroup.alpha = 0f; _view.DotsGroup.gameObject.SetActive(false); }
            if (_view.BookGroup != null) { _view.BookGroup.alpha = 0f; _view.BookGroup.gameObject.SetActive(false); }
            if (_view.CommentGroup != null) { _view.CommentGroup.alpha = 0f; _view.CommentGroup.gameObject.SetActive(false); }
            if (_view.RejectionGroup != null) { _view.RejectionGroup.alpha = 0f; _view.RejectionGroup.gameObject.SetActive(false); }
            _currentActive = null;
        }
    }
}
