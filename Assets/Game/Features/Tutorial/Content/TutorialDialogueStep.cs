using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dialogue;
using Game.Tutorial;
using Game.Tutorial.API;
using Game.UI;
using UnityEngine;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDialogueStep : ITutorialStep
    {
        private readonly IUIManager _ui;
        private readonly string _dialogueId;

        public TutorialDialogueStep(string id, IUIManager ui, string dialogueId)
        {
            Id = id;
            _ui = ui;
            _dialogueId = dialogueId;
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (_ui == null)
            {
                Debug.LogWarning($"{TutorialLog.Prefix} dialogue step '{Id}' skipped: IUIManager is missing.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_dialogueId))
            {
                Debug.LogWarning($"{TutorialLog.Prefix} dialogue step '{Id}' skipped: dialogue id is empty.");
                return;
            }

            DialogWindow window;
            try
            {
                window = await _ui.ShowAsync<DialogWindow>(
                    new DialogWindowArgs(new DialoguePayload(_dialogueId)),
                    ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TutorialLog.Prefix} dialogue step '{Id}' skipped: failed to show dialogue '{_dialogueId}'. {e}");
                return;
            }

            if (window == null)
            {
                Debug.LogWarning($"{TutorialLog.Prefix} dialogue step '{Id}' skipped: dialogue window was not shown.");
                return;
            }

            if (!window.IsShown)
                return;

            var closed = new UniTaskCompletionSource();
            void OnClosed(IWindowController _) => closed.TrySetResult();

            window.Closed += OnClosed;
            try
            {
                if (!window.IsShown)
                    return;

                await closed.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                window.Closed -= OnClosed;
            }
        }
    }
}
