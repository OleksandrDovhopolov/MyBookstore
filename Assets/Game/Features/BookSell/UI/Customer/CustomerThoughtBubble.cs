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
        private const float DotsFrameInterval = 0.3f;   // seconds per "..." frame
        private const int MaxDots = 5;
        private static readonly Vector3 ScaleInFrom = Vector3.one * 0.5f;
        private static readonly Vector3 ScaleInTo = Vector3.one;

        private CustomerThoughtBubbleView _view;
        private CanvasGroup _currentActive;
        private CancellationTokenSource _stateCts;
        private bool _dotsAnimating;

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
                await CrossfadeAsync(next, ct);

                // Per-state secondary animation (subtle scale-in for book / rejection).
                await PlaySecondaryAsync(state, ct);

                // Run the animated dots (in the dedicated Dots text) until the next state cancels _stateCts.
                if (_dotsAnimating)
                    RunThinkingDotsAsync(ct).Forget();

                State = state;
            }
            catch (OperationCanceledException) { /* superseded — swallow */ }
        }

        private void ApplyContent(CustomerThoughtState state, CustomerThoughtPayload payload)
        {
            if (_view == null) return;

            // State text shows the phase label as before (e.g. "moving" for Approaching). ThinkingNext
            // forces an empty label so the old "Book locked" never shows.
            var label = state == CustomerThoughtState.ThinkingNext
                ? string.Empty
                : ResolveStateLabel(state, payload);
            if (_view.StateText != null) _view.StateText.text = label;

            // Animated dots run in their own Dots text, only when a thinking state has no label to show
            // (Approaching keeps its "moving" text; Browsing / ThinkingNext get the dots).
            _dotsAnimating = IsThinking(state) && string.IsNullOrEmpty(label);
            if (_view.DotsText != null)
            {
                _view.DotsText.gameObject.SetActive(_dotsAnimating);
                if (_dotsAnimating) _view.DotsText.text = ".";   // seed to avoid a blank first frame
            }

            ApplySaleIcons(state);

            switch (state)
            {
                case CustomerThoughtState.BookPicked:
                    if (_view.BookIcon != null) _view.BookIcon.sprite = payload.BookSprite;
                    break;
                case CustomerThoughtState.Comment:
                case CustomerThoughtState.PassiveSaleFailed:
                case CustomerThoughtState.PurchaseCompleted:
                    if (_view.CommentText != null) _view.CommentText.text = payload.CommentText ?? string.Empty;
                    break;
                case CustomerThoughtState.Rejected:
                    if (_view.RejectedBookIcon != null) _view.RejectedBookIcon.sprite = payload.RejectedBookSprite;
                    if (_view.ReplacementBookIcon != null) _view.ReplacementBookIcon.sprite = payload.ReplacementBookSprite;
                    break;
            }
        }

        // Stage 1: passive sale result shows a Success/Fail image instead of the State text.
        // Success ← Comment (passive sale happened), Fail ← PassiveSaleFailed. Other states keep the text.
        private void ApplySaleIcons(CustomerThoughtState state)
        {
            var showSuccess = state == CustomerThoughtState.Comment;
            var showFail = state == CustomerThoughtState.PassiveSaleFailed;

            if (_view.SuccessIcon != null) _view.SuccessIcon.gameObject.SetActive(showSuccess);
            if (_view.FailIcon != null) _view.FailIcon.gameObject.SetActive(showFail);

            if (_view.StateText != null)
                _view.StateText.gameObject.SetActive(!showSuccess && !showFail);
        }

        private static bool IsThinking(CustomerThoughtState state)
            => state == CustomerThoughtState.Thinking || state == CustomerThoughtState.ThinkingNext;

        // Animates the Dots text "." -> "....." (1..MaxDots) and loops, until _stateCts cancels on the
        // next state change (or on detach). Fire-and-forget under the state token.
        private async UniTaskVoid RunThinkingDotsAsync(CancellationToken ct)
        {
            try
            {
                var count = 1;
                while (!ct.IsCancellationRequested)
                {
                    if (_view != null && _view.DotsText != null)
                        _view.DotsText.text = new string('.', count);

                    count = count >= MaxDots ? 1 : count + 1;
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(DotsFrameInterval), ignoreTimeScale: true, cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { /* superseded — stop */ }
        }

        private static string ResolveStateLabel(CustomerThoughtState state, CustomerThoughtPayload payload)
        {
            if (!string.IsNullOrEmpty(payload.CommentText))
                return payload.CommentText;

            return state switch
            {
                CustomerThoughtState.Thinking => string.Empty,      // dots indicator instead of text
                CustomerThoughtState.ThinkingNext => string.Empty,  // dots indicator instead of text
                CustomerThoughtState.BookPicked => "Active purchase",
                CustomerThoughtState.Comment => "Bought book",
                CustomerThoughtState.Rejected => "Active purchase",
                CustomerThoughtState.PassiveSaleFailed => "Failed",
                CustomerThoughtState.PurchaseCompleted => "Done shopping",
                _ => string.Empty
            };
        }

        private CanvasGroup ResolveGroup(CustomerThoughtState state) => state switch
        {
            CustomerThoughtState.Thinking => _view?.DotsGroup,
            CustomerThoughtState.ThinkingNext => _view?.DotsGroup,
            CustomerThoughtState.BookPicked => _view?.BookGroup,
            CustomerThoughtState.Comment => _view?.CommentGroup,
            CustomerThoughtState.Rejected => _view?.RejectionGroup,
            CustomerThoughtState.PassiveSaleFailed => _view?.CommentGroup,
            CustomerThoughtState.PurchaseCompleted => _view?.CommentGroup,
            _ => null,
        };

        private async UniTask CrossfadeAsync(CanvasGroup to, CancellationToken ct)
        {
            // Supersede-safe: snap every other sub-view off immediately. Rapid state changes (e.g.
            // AwaitingHelp -> InMinigame in a single tick) cancel an in-flight fade mid-way (OCE); hiding
            // the others here guarantees we never leave two sub-views (dots + book) overlapping, which the
            // previous fade-out-then-fade-in sequence did when cancelled.
            HideGroupsExcept(to);

            _currentActive = to;
            if (to == null) return;

            to.gameObject.SetActive(true);
            if (to.alpha < 1f)
                await TweenAsync.LerpAlphaAsync(to, to.alpha, 1f, CrossfadeDuration, ct);
            else
                to.alpha = 1f;
        }

        private void HideGroupsExcept(CanvasGroup keep)
        {
            if (_view == null) return;
            HideGroup(_view.DotsGroup, keep);
            HideGroup(_view.BookGroup, keep);
            HideGroup(_view.CommentGroup, keep);
            HideGroup(_view.RejectionGroup, keep);
        }

        private static void HideGroup(CanvasGroup group, CanvasGroup keep)
        {
            if (group == null || group == keep) return;
            group.alpha = 0f;
            group.gameObject.SetActive(false);
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
            if (_view.SuccessIcon != null) _view.SuccessIcon.gameObject.SetActive(false);
            if (_view.FailIcon != null) _view.FailIcon.gameObject.SetActive(false);
            if (_view.DotsText != null) _view.DotsText.gameObject.SetActive(false);
            _dotsAnimating = false;
            _currentActive = null;
        }
    }
}
