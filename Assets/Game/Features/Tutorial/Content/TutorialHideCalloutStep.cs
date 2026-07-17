using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;
using Game.Tutorial.Presentation;

namespace Game.Tutorial.Content
{
    public sealed class TutorialHideCalloutStep : ITutorialStep
    {
        private readonly TutorialOverlayController _overlay;

        public TutorialHideCalloutStep(string id, TutorialOverlayController overlay)
        {
            Id = id;
            _overlay = overlay;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
        {
            _overlay.HideCallout();
            return UniTask.CompletedTask;
        }
    }
}
