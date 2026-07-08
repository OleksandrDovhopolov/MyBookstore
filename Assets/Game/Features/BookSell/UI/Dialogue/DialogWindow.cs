using System.Collections.Generic;
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
    /// Modal dialogue window (GAME-6 §Этап 5, A3). Mirrors <see cref="RecommendationMinigameWindow"/>: the
    /// gameplay-scoped controller + payload arrive via <see cref="DialogWindowArgs"/>, while the content graph
    /// is resolved through the bootstrap-scope <see cref="IConfigsService"/> (so the window also works from the
    /// debug cheat with a null controller, §A6).
    ///
    /// Runs a <see cref="DialogueEngine"/> over the resolved <see cref="DialogueConfig"/>. Non-terminal nodes
    /// render up to 3 option buttons; a terminal node renders its lines plus one generated "Continue" button
    /// (otherwise the player would be stuck on the final line with no way to close).
    ///
    /// <see cref="Complete"/> is the single completion point AND the anti-hang safety net: it sets
    /// <c>_completed</c> BEFORE calling <see cref="ISalesDayController.CompleteDialogue"/> so the
    /// hide/dispose fallbacks do not double-fire, and <see cref="OnHideStart"/>/<see cref="OnDispose"/> call it
    /// if the window is torn down without ending — guaranteeing the interaction lock is always released.
    /// </summary>
    [Window("DialogWindow", WindowType.Popup)]
    public sealed class DialogWindow : WindowController<DialogWindowView>
    {
        private const string LogPrefix = "[DialogWindow]";
        private const string ContinueLabel = "Продолжить";

        private IConfigsService _configs;
        private ISalesDayController _controller;
        private DialoguePayload _payload;
        private DialogueEngine _engine;
        private bool _completed;

        [Inject]
        public void InjectConfigs(IConfigsService configs) => _configs = configs;

        protected override void OnShowStart()
        {
            var args = Arguments as DialogWindowArgs;
            _controller = args?.Controller;
            _payload = args?.Payload;
            _completed = false;

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
            RenderCurrentNode();
        }

        protected override void OnHideStart(bool isClosed)
        {
            base.OnHideStart(isClosed);
            // Window closed from outside (X / scene teardown) without ending the graph — release the lock.
            if (!_completed)
                Complete();
        }

        protected override void OnDispose()
        {
            if (!_completed)
                Complete();

            _engine = null;
            _controller = null;
            _payload = null;
        }

        private void RenderCurrentNode()
        {
            var node = _engine.Current;
            View.SetLines(node.Lines);

            if (_engine.IsTerminal)
            {
                // Terminal node: no choice left. Offer a single generated "Continue" button that ends.
                View.SetOptions(new[] { ContinueLabel }, _ => CompleteAndClose());
                return;
            }

            var labels = new List<string>(node.Options.Length);
            foreach (var option in node.Options)
                labels.Add(option?.Text ?? string.Empty);

            View.SetOptions(labels, OnOptionPicked);
        }

        private void OnOptionPicked(int optionIndex)
        {
            switch (_engine.Choose(optionIndex))
            {
                case ChooseResult.Advanced:
                    RenderCurrentNode();
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
