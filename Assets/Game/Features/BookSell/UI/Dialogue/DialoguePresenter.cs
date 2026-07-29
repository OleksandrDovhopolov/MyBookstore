using System;
using System.Threading;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Services;
using Cysharp.Threading.Tasks;
using Dialogue;
using Game.UI;
using UnityEngine;
using VContainer.Unity;

namespace Book.Sell.UI
{
    /// <summary>
    /// Owns the lifecycle of <see cref="DialogWindow"/> (GAME-6 §Этап 5, A5) — the dialogue counterpart of
    /// <see cref="RecommendationMinigamePresenter"/>. Listens for <see cref="ISalesDayController.DialogueStarted"/>
    /// and opens the window, handing it the payload plus <see cref="ISalesDayController.CompleteDialogue"/> as a
    /// neutral completion callback via <see cref="DialogWindowArgs"/> (the bootstrap-scoped window factory cannot
    /// inject the gameplay-scope controller, and the Dialogue module must not depend on the sales feature).
    ///
    /// Completion is owned by the window (it invokes the callback on end/teardown → <c>CompleteDialogue()</c>);
    /// this presenter only opens it and provides the anti-hang safety net if the open itself fails — a missing
    /// <see cref="IUIManager"/>, a null window, or an exception all call <c>CompleteDialogue()</c> so the held
    /// interaction lock is released and the day never hangs.
    ///
    /// The day pauses for free: the domain DialogStep holds the lock and the sim tick short-circuits on a
    /// held lock, so no IsWindowOpen gate or SalesScreenView change is needed.
    /// </summary>
    public sealed class DialoguePresenter : IStartable, IDisposable
    {
        private const string LogPrefix = "[DialoguePresenter]";

        private readonly ISalesDayController _controller;
        private readonly IUIManager _uiManager;
        private readonly IDeliveredDialoguesService _delivered;
        private readonly DialogueQuestActivator _questActivator;
        private readonly CancellationTokenSource _cts = new();

        public DialoguePresenter(
            ISalesDayController controller,
            IUIManager uiManager = null,
            IDeliveredDialoguesService delivered = null,
            DialogueQuestActivator questActivator = null)
        {
            _controller = controller;
            _uiManager = uiManager;
            _delivered = delivered;
            _questActivator = questActivator;
        }

        public void Start()
        {
            if (_controller != null)
                _controller.DialogueStarted += OnDialogueStarted;
        }

        public void Dispose()
        {
            if (_controller != null)
                _controller.DialogueStarted -= OnDialogueStarted;

            _cts.Cancel();
            _cts.Dispose();
        }

        private void OnDialogueStarted(Book.Sell.Domain.Customer customer, DialoguePayload payload)
            => OpenAsync(payload).Forget();

        private async UniTaskVoid OpenAsync(DialoguePayload payload)
        {
            if (_uiManager == null)
            {
                Debug.LogWarning($"{LogPrefix} IUIManager was not injected — cannot open the dialogue window.");
                _controller?.CompleteDialogue();   // don't leave the day paused with no UI to resolve it
                return;
            }

            try
            {
                var window = await _uiManager.ShowAsync<DialogWindow>(
                    new DialogWindowArgs(
                        payload,
                        () => CompleteDialogueAfterQuestActivation(payload?.DialogueId).Forget()),
                    _cts.Token);

                if (window == null)
                {
                    _controller?.CompleteDialogue();
                    return;
                }

                // Fire-once: any window close (including external X / teardown, which the window's OnHideStart
                // still turns into Complete()) counts the dialogue as delivered so the quest character does not
                // return while its quest stays Active. CancellationToken.None on purpose — this progress write
                // must NOT be tied to the presenter/scene lifetime (_cts), since Closed often fires at teardown.
                window.Closed += _ =>
                    _delivered?.MarkDeliveredDeferredAsync(payload.DialogueId, CancellationToken.None).Forget();
            }
            catch (OperationCanceledException)
            {
                _controller?.CompleteDialogue();
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to open the dialogue window: {ex}");
                _controller?.CompleteDialogue();
            }
        }

        private async UniTaskVoid CompleteDialogueAfterQuestActivation(string dialogueId)
        {
            try
            {
                if (_questActivator != null)
                    await _questActivator.ActivateForDialogueAsync(dialogueId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogPrefix} Failed to activate quest for dialogue '{dialogueId}': {ex}");
            }
            finally
            {
                _controller?.CompleteDialogue();
            }
        }
    }
}
