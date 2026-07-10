using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Tutorial.Presentation;
using Game.UI;

namespace Game.Tutorial.Steps
{
    /// <summary>
    /// Full-screen dim + text; advances on any tap. Holds a UIManager manual lock for the step so no other
    /// system opens/closes a window mid-text (the blackout blocks input; the lock guards window flow).
    /// </summary>
    public sealed class ShowTextStepHandler : ITutorialStepHandler
    {
        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;

        public ShowTextStepHandler(TutorialOverlayController overlay, IUIManager ui)
        {
            _overlay = overlay;
            _ui = ui;
        }

        public string Type => TutorialStepTypes.ShowText;

        public async UniTask ExecuteAsync(TutorialStepConfig step, CancellationToken ct)
        {
            try
            {
                using (_ui.SetManualLock(this))
                    await _overlay.ShowTextAndWaitTapAsync(step?.Text, step?.Placement, ct);
            }
            finally
            {
                _overlay.HideText();
            }
        }
    }
}
