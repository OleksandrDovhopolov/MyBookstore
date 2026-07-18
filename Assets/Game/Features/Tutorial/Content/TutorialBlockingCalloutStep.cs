using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;
using Game.UI;
using MessagePipe;

namespace Game.Tutorial.Content
{
    public sealed class TutorialBlockingCalloutStep : ITutorialStep
    {
        private readonly IUIManager _ui;
        private readonly TutorialOverlayController _overlay;
        private readonly IPublisher<SalesPauseRequested> _pausePublisher;
        private readonly Func<bool> _gate;
        private readonly Func<string> _textFactory;
        private readonly string _placement;

        public TutorialBlockingCalloutStep(
            string id,
            IUIManager ui,
            TutorialOverlayController overlay,
            IPublisher<SalesPauseRequested> pausePublisher,
            Func<bool> gate,
            Func<string> textFactory,
            string placement)
        {
            Id = id;
            _ui = ui;
            _overlay = overlay;
            _pausePublisher = pausePublisher;
            _gate = gate;
            _textFactory = textFactory;
            _placement = placement;
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            if (_gate?.Invoke() != true)
                return;

            var text = _textFactory?.Invoke();
            if (string.IsNullOrEmpty(text))
                return;

            _pausePublisher.Publish(new SalesPauseRequested(true));
            try
            {
                using (_ui.SetManualLock(this))
                    await _overlay.ShowTextAndWaitTapAsync(text, _placement, ct);
            }
            finally
            {
                _overlay.HideBlackout();
                _pausePublisher.Publish(new SalesPauseRequested(false));
            }
        }
    }
}
