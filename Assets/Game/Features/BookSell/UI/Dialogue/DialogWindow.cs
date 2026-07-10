using System;
using System.Collections.Generic;
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
    /// Plays a node's replies as a top-to-bottom feed, one at a time (typewriter): a screen click reveals the
    /// next reply; clicks are ignored while one is still typing. When a node's replies are exhausted, if the
    /// node offers <c>Options</c> the answer buttons are shown and the pick advances the <see cref="DialogueEngine"/>
    /// to the next node (its replies are appended to the same feed); a terminal node closes on the next click.
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
        private DialogueEngine _engine;

        // Auto speaker→side map for the whole conversation: first distinct speaker → left, second → right
        // (2-character assumption). Persists across nodes so a speaker keeps its side.
        private readonly Dictionary<string, bool> _speakerSide = new(StringComparer.Ordinal);

        private DialogueLineConfig[] _lines;
        private int _lineIndex;
        private bool _revealing;
        private bool _awaitingChoice;
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
            _awaitingChoice = false;
            _lineIndex = 0;
            _speakerSide.Clear();

            // Fresh CTS every show: windows are cached & reused, so a disposed field token from a prior show
            // would poison the next open.
            _revealCts = new CancellationTokenSource();

            View.ClearLines();
            View.HideOptions();

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

            _engine = new DialogueEngine(config);

            // Skip is a stub this iteration — keep it visible-but-inert so it doesn't look like a live control.
            if (View.SkipButton != null) View.SkipButton.interactable = false;

            Subscribe();
            PlayCurrentNode();   // reveal the entry node's replies (or its options if it has none)
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

            _engine = null;
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
            if (_awaitingChoice) return;            // options are up — only the option buttons act
            if (_revealing) return;                 // block clicks while a reply is still typing
            if (_lineIndex < (_lines?.Length ?? 0))
                RevealNextAsync().Forget();
            else
                CompleteAndClose();                 // terminal node's replies shown — a click ends it
        }

        // Skip is intentionally inert this iteration (see class doc). TODO: fast-forward / close.
        private void OnSkipClicked()
        {
            CloseAsync().Forget();
        }

        // Loads the engine's current node into the feed: reveal its replies, or (if it has none) go straight
        // to its options / terminal handling.
        private void PlayCurrentNode()
        {
            _lines = _engine.Current.Lines ?? System.Array.Empty<DialogueLineConfig>();
            _lineIndex = 0;

            if (_lines.Length == 0)
                MaybeShowOptionsOrEndNode();
            else
                RevealNextAsync().Forget();
        }

        private async UniTaskVoid RevealNextAsync()
        {
            if (_lineIndex >= (_lines?.Length ?? 0)) return;

            _revealing = true;
            var line = _lines[_lineIndex];
            _lineIndex++;

            var view = View.AppendLine(line?.Speaker, line?.Text, SideFor(line?.Speaker));
            if (view != null && _revealCts != null)
                await view.RevealAsync(_revealCts.Token);

            _revealing = false;

            // Last reply of the node just finished — offer the choice (or wait for a close-click if terminal).
            if (_lineIndex >= (_lines?.Length ?? 0))
                MaybeShowOptionsOrEndNode();
        }

        // Current node's replies are exhausted (or it had none): show its answer options, or do nothing for a
        // terminal node (a screen click will close it via OnScreenClicked).
        private void MaybeShowOptionsOrEndNode()
        {
            if (_engine == null || _engine.IsTerminal) return;

            var options = _engine.Current.Options;
            var labels = new string[options.Length];
            for (var i = 0; i < options.Length; i++)
                labels[i] = options[i]?.Text ?? string.Empty;

            View.ShowOptions(labels, OnOptionPicked);
            _awaitingChoice = true;
        }

        private void OnOptionPicked(int optionIndex)
        {
            if (!_awaitingChoice) return;   // guard against a double / queued click firing Choose twice
            _awaitingChoice = false;
            View.HideOptions();

            switch (_engine.Choose(optionIndex))
            {
                case ChooseResult.Advanced:
                    PlayCurrentNode();      // append the next node's replies to the same feed
                    break;

                case ChooseResult.Ended:
                    CompleteAndClose();
                    break;

                case ChooseResult.UnknownTarget:
                    // Content error: option points at a missing node. Don't strand the player — treat as end.
                    Debug.LogError($"{LogPrefix} Option {optionIndex} on node '{_engine.Current.NodeId}' " +
                                   $"('{_payload.DialogueId}') targets an unknown node — ending the dialogue.");
                    CompleteAndClose();
                    break;
            }
        }

        // First distinct speaker → left (false), second → right (true), third+ → left. Stable for a speaker
        // across the whole conversation.
        private bool SideFor(string speaker)
        {
            speaker ??= string.Empty;
            if (_speakerSide.TryGetValue(speaker, out var isRight))
                return isRight;

            isRight = _speakerSide.Count == 1;
            _speakerSide[speaker] = isRight;
            return isRight;
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
