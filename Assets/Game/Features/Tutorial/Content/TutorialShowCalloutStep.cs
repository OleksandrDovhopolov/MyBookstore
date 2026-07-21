using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;

namespace Game.Tutorial.Content
{
    public sealed class TutorialShowCalloutStep : ITutorialStep
    {
        private readonly TutorialOverlayController _overlay;
        private readonly Func<string> _textFactory;
        private readonly string _placement;

        public TutorialShowCalloutStep(string id, TutorialOverlayController overlay, string text, string placement)
            : this(id, overlay, () => text, placement)
        {
        }

        public TutorialShowCalloutStep(
            string id,
            TutorialOverlayController overlay,
            Func<string> textFactory,
            string placement)
        {
            Id = id;
            _overlay = overlay;
            _textFactory = textFactory;
            _placement = placement;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            var text = _textFactory?.Invoke();
            if (string.IsNullOrEmpty(text))
                return UniTask.CompletedTask;

            _overlay.ShowCallout(text, _placement);
            return UniTask.CompletedTask;
        }
    }
}
