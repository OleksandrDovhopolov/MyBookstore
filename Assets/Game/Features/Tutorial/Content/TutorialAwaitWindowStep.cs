using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialAwaitWindowStep : ITutorialStep
    {
        private const int PollMs = 250;

        private readonly Func<bool> _isShown;

        public TutorialAwaitWindowStep(string id, Func<bool> isShown)
        {
            Id = id;
            _isShown = isShown ?? throw new ArgumentNullException(nameof(isShown));
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            while (!_isShown())
                await UniTask.Delay(PollMs, cancellationToken: ct);
        }
    }
}
