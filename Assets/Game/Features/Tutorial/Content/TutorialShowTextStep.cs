using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;

namespace Game.Tutorial.Content
{
    public sealed class TutorialShowTextStep : ITutorialStep
    {
        private readonly TutorialOverlayController _overlay;
        private readonly IUIManager _ui;
        private readonly string _text;
        private readonly string _placement;

        public TutorialShowTextStep(
            string id,
            TutorialOverlayController overlay,
            IUIManager ui,
            string text,
            string placement)
        {
            Id = id;
            _overlay = overlay;
            _ui = ui;
            _text = text;
            _placement = placement;
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            try
            {
                using (_ui.SetManualLock(this))
                    await _overlay.ShowTextAndWaitTapAsync(_text, _placement, ct);
            }
            finally
            {
                _overlay.HideText();
            }
        }
    }
}
