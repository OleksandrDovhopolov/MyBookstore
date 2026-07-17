using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialDelayStep : ITutorialStep
    {
        private readonly TimeSpan _delay;

        public TutorialDelayStep(string id, TimeSpan delay)
        {
            Id = id;
            _delay = delay;
        }

        public string Id { get; }

        public UniTask ExecuteAsync(CancellationToken ct)
            => UniTask.Delay(_delay, cancellationToken: ct);
    }
}
