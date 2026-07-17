using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialAwaitWindowStep : ITutorialStep
    {
        private const int PollMs = 250;

        private readonly Func<bool> _predicate;

        public TutorialAwaitWindowStep(string id, Func<bool> predicate)
        {
            Id = id;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public string Id { get; }

        public async UniTask ExecuteAsync(CancellationToken ct)
        {
            while (!_predicate())
                await UniTask.Delay(PollMs, cancellationToken: ct);
        }
    }
}
