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
        private readonly bool _hideTextAfterTap;
        private readonly bool _dimBackground;
        private readonly bool _lockUi;

        public TutorialBlockingCalloutStep(
            string id,
            IUIManager ui,
            TutorialOverlayController overlay,
            IPublisher<SalesPauseRequested> pausePublisher,
            Func<bool> gate,
            Func<string> textFactory,
            string placement,
            bool hideTextAfterTap = false,
            bool dimBackground = true,
            bool lockUi = true)
        {
            Id = id;
            _ui = ui;
            _overlay = overlay;
            _pausePublisher = pausePublisher;
            _gate = gate;
            _textFactory = textFactory;
            _placement = placement;
            _hideTextAfterTap = hideTextAfterTap;
            _dimBackground = dimBackground;
            _lockUi = lockUi;
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
                if (_lockUi)
                {
                    using (_ui.SetManualLock(this))
                        await _overlay.ShowTextAndWaitTapAsync(text, _placement, _dimBackground, ct);
                }
                else
                {
                    await _overlay.ShowTextAndWaitTapAsync(text, _placement, _dimBackground, ct);
                }
            }
            finally
            {
                _overlay.HideBlackout();
                if (_hideTextAfterTap)
                    _overlay.HideCallout();
                _pausePublisher.Publish(new SalesPauseRequested(false));
            }
        }
    }
}
