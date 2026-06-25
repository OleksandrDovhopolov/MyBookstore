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
        private const float DotsFrameInterval = 0.4f;   // seconds per "..." frame
        private const int MaxDots = 5;

        private CustomerThoughtBubbleView _view;
        // Owns the dots loop independently of state changes, so the animation keeps running smoothly
        // across consecutive thinking states (Thinking -> ThinkingNext) instead of restarting.
        private CancellationTokenSource _dotsCts;

        public CustomerThoughtState State { get; private set; } = CustomerThoughtState.None;

        protected override void OnAttached()
        {
            _view = GetComponent<CustomerThoughtBubbleView>();
            DeactivateAll();

            if (CanvasGroup != null) CanvasGroup.alpha = 1f;
        }

        protected override async UniTask OnDetachAsync(CancellationToken ct)
        {
            StopDots();

            if (CanvasGroup != null)
            {
                await TweenAsync.LerpAlphaAsync(CanvasGroup, CanvasGroup.alpha, 0f, CrossfadeDuration, ct);
            }
        }

        public UniTask SetStateAsync(
            CustomerThoughtState state,
            CustomerThoughtPayload payload = null,
            CancellationToken externalCt = default)
        {
            payload ??= CustomerThoughtPayload.Empty;

            ApplyContent(state, payload);   // starts/stops/keeps the dots loop as needed
            State = state;
            return UniTask.CompletedTask;
        }

        private void ApplyContent(CustomerThoughtState state, CustomerThoughtPayload payload)
        {
            if (_view == null) return;

            // One visual channel per state:
            //   Thinking      -> animated dots   (browse / _browseDuration)
            //   ThinkingNext  -> book icon       (locked / _passiveCommitDelay)
            //   Comment       -> success icon
            //   PassiveSaleFailed -> fail icon
            //   everything else   -> State text label
            var showDots = state == CustomerThoughtState.Thinking;
            var showSuccess = state == CustomerThoughtState.Comment;
            var showFail = state == CustomerThoughtState.PassiveSaleFailed;
            // Book icon shows the locked book (ThinkingNext) and, on a failed attempt, the missed genre
            // (alongside the Fail icon) when a sprite is available.
            var showBook = state == CustomerThoughtState.ThinkingNext
                           || (showFail && payload.BookSprite != null);
            var showText = !showDots && !showBook && !showSuccess && !showFail;

            UpdateDots(showDots);

            if (_view.BookIcon != null)
            {
                if (showBook && payload.BookSprite != null) _view.BookIcon.sprite = payload.BookSprite;
                _view.BookIcon.gameObject.SetActive(showBook);
            }

            if (_view.SuccessIcon != null) _view.SuccessIcon.gameObject.SetActive(showSuccess);
            if (_view.FailIcon != null) _view.FailIcon.gameObject.SetActive(showFail);

            if (_view.StateText != null)
            {
                _view.StateText.text = showText ? ResolveStateLabel(state, payload) : string.Empty;
                _view.StateText.gameObject.SetActive(showText);
            }
        }

        private void UpdateDots(bool animate)
        {
            if (!animate)
            {
                StopDots();
                return;
            }

            if (_view != null && _view.DotsText != null) _view.DotsText.gameObject.SetActive(true);

            // Already running (the previous state was also a thinking-dots state): keep it going so the
            // animation doesn't visibly reset on Thinking -> ThinkingNext.
            if (_dotsCts != null) return;

            if (_view != null && _view.DotsText != null) _view.DotsText.text = ".";   // seed
            _dotsCts = new CancellationTokenSource();
            RunThinkingDotsAsync(_dotsCts.Token).Forget();
        }

        private void StopDots()
        {
            _dotsCts?.Cancel();
            _dotsCts?.Dispose();
            _dotsCts = null;
            if (_view != null && _view.DotsText != null) _view.DotsText.gameObject.SetActive(false);
        }

        // Animates the Dots text "." -> "....." (1..MaxDots) and loops, until _dotsCts cancels (leaving a
        // thinking state or on detach). Fire-and-forget under the dots token.
        private async UniTaskVoid RunThinkingDotsAsync(CancellationToken ct)
        {
            try
            {
                var count = 1;
                while (!ct.IsCancellationRequested)
                {
                    if (_view == null) return;   // bubble destroyed — stop the loop
                    if (_view.DotsText != null)
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
                CustomerThoughtState.PassiveSaleFailed => "Failed",
                CustomerThoughtState.PurchaseCompleted => "Done shopping",
                _ => string.Empty
            };
        }

        private void DeactivateAll()
        {
            if (_view == null) return;
            if (_view.SuccessIcon != null) _view.SuccessIcon.gameObject.SetActive(false);
            if (_view.FailIcon != null) _view.FailIcon.gameObject.SetActive(false);
            if (_view.BookIcon != null) _view.BookIcon.gameObject.SetActive(false);
            StopDots();
        }
    }
}
