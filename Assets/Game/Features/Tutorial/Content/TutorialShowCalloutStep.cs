using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;

namespace Game.Tutorial.Content
{
    public sealed class TutorialShowCalloutStep : ITutorialStep
    {
        private readonly TutorialOverlayController _overlay;
        private readonly string _text;
        private readonly string _placement;

        public TutorialShowCalloutStep(string id, TutorialOverlayController overlay, string text, string placement)
        {
            Id = id;
            _overlay = overlay;
            _text = text;
            _placement = placement;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            _overlay.ShowCallout(_text, _placement);
            return UniTask.CompletedTask;
        }
    }
}
