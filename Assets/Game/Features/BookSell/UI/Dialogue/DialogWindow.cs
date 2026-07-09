using System.Threading;
using Book.Sell.API;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Book.Sell.UI
{
    /// <summary>
    /// Modal dialogue window (GAME-6). Mirrors <see cref="RecommendationMinigameWindow"/>: the gameplay-scoped
    /// controller + payload arrive via <see cref="DialogWindowArgs"/>, while the content graph is resolved
    /// through the bootstrap-scope <see cref="IConfigsService"/> (so the window also works from the debug cheat
    /// with a null controller).
    ///
    /// Presents the entry node's replies as a top-to-bottom feed, one at a time (typewriter): a screen click
    /// reveals the next reply; clicks are ignored while one is still typing; after the last reply a click closes
    /// the window. Answer options (choices) are NOT rendered this iteration — the graph's <c>Options</c> stay in
    /// config for a later iteration, so a choice dialogue just shows its root replies and closes.
    ///
    /// <see cref="Complete"/> is the single completion point AND the anti-hang safety net: it sets
    /// <c>_completed</c> BEFORE calling <see cref="ISalesDayController.CompleteDialogue"/> so the hide/dispose
    /// fallbacks do not double-fire, and <see cref="OnHideStart"/>/<see cref="OnDispose"/> call it if the window
    /// is torn down without ending — guaranteeing the interaction lock is always released.
    /// </summary>
    [Window("DialogWindow", WindowType.Popup)]
    public sealed class DialogWindow : WindowController<DialogWindowView>
    {
        private const string LogPrefix = "[DialogWindow]";

        private IConfigsService _configs;
        private ISalesDayController _controller;
        private DialoguePayload _payload;

        private DialogueLineConfig[] _lines;
        private int _lineIndex;
        private bool _revealing;
        private bool _completed;
        private bool _subscribed;
        private CancellationTokenSource _revealCts;

        [Inject]
        public void InjectConfigs(IConfigsService configs) => _configs = configs;

        protected override void OnShowStart()
        {
            var args = Arguments as DialogWindowArgs;
            _controller = args?.Controller;
            _payload = args?.Payload;
            _completed = false;
            _revealing = false;
            _lineIndex = 0;

            // Fresh CTS every show: windows are cached & reused, so a disposed field token from a prior show
            // would poison the next open.
            _revealCts = new CancellationTokenSource();

            View.ClearLines();

            if (_payload == null)
            {
                Debug.LogError($"{LogPrefix} No DialoguePayload in args — closing.");
                CompleteAndClose();
                return;
            }

            var config = _configs?.Get<DialogueConfig>(_payload.DialogueId);
            if (config == null || config.Nodes == null || config.Nodes.Length == 0)
            {
                // Get already logs a not-found warning. Fail-safe: release the lock and close.
                Debug.LogError($"{LogPrefix} DialogueConfig '{_payload.DialogueId}' missing or has no nodes — closing.");
                CompleteAndClose();
                return;
            }

            // Engine resolves + validates the entry node (choices come next iteration; for now we play the
            // entry node's replies).
            var engine = new DialogueEngine(config);
            _lines = engine.Current.Lines ?? System.Array.Empty<DialogueLineConfig>();

            // Skip is a stub this iteration — keep it visible-but-inert so it doesn't look like a live control.
            if (View.SkipButton != null) View.SkipButton.interactable = false;

            Subscribe();
            RevealNextAsync().Forget();   // auto-show the first reply; clicks drive the rest
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            CancelReveal();
            Unsubscribe();
            // Window closed from outside (X / scene teardown) without finishing — release the lock.
            if (!_completed)
                Complete();
        }

        protected override void OnDispose()
        {
            CancelReveal();
            Unsubscribe();
            if (!_completed)
                Complete();

            _lines = null;
            _controller = null;
            _payload = null;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            View.ScreenClicked += OnScreenClicked;
            View.SkipClicked += OnSkipClicked;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            View.ScreenClicked -= OnScreenClicked;
            View.SkipClicked -= OnSkipClicked;
            _subscribed = false;
        }

        private void OnScreenClicked()
        {
            if (_revealing) return;                 // block clicks while a reply is still typing
            if (_lineIndex < (_lines?.Length ?? 0))
                RevealNextAsync().Forget();
            else
                CompleteAndClose();                 // all replies shown — a click ends the conversation
        }

        // Skip is intentionally inert this iteration (see class doc). TODO: fast-forward / close.
        private void OnSkipClicked()
        {
            CloseAsync().Forget();
        }

        private async UniTaskVoid RevealNextAsync()
        {
            if (_lineIndex >= (_lines?.Length ?? 0)) return;

            _revealing = true;
            var line = _lines[_lineIndex];
            _lineIndex++;

            var view = View.AppendLine(line?.Speaker, line?.Text);
            if (view != null && _revealCts != null)
                await view.RevealAsync(_revealCts.Token);

            _revealing = false;
        }

        private void CancelReveal()
        {
            _revealCts?.Cancel();
            _revealCts?.Dispose();
            _revealCts = null;
        }

        // Single completion point + anti-hang safety net. Sets the flag BEFORE notifying the controller so the
        // OnHideStart/OnDispose fallbacks cannot fire a second CompleteDialogue.
        private void Complete()
        {
            if (_completed) return;
            _completed = true;
            _controller?.CompleteDialogue();
        }

        private void CompleteAndClose()
        {
            Complete();
            // Forget (not synchronous await): ShowAsync may still hold the UIManager show gate, so a
            // synchronous CloseAsync would deadlock — the hide is deferred behind the gate instead.
            CloseAsync().Forget();
        }
    }
}
